using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.Domain.Models;

namespace PppPricing.API.Services;

public record EffectiveMultiplierInput(string RegionCode, string? CurrencyCode);

public class EffectiveMultiplierResult
{
    public string RegionCode { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
    public bool IsFallback { get; set; }
    public string? FallbackReason { get; set; }
    public string? Source { get; set; }
    public int SourceRegionCount { get; set; }
}

public interface IEffectiveMultiplierService
{
    Task<Dictionary<string, EffectiveMultiplierResult>> ResolveForRegionsAsync(
        IEnumerable<EffectiveMultiplierInput> regions,
        PricingIndexType indexType,
        Guid? userId,
        string? planType = null);
}

public class EffectiveMultiplierService : IEffectiveMultiplierService
{
    private readonly ApplicationDbContext _context;

    public EffectiveMultiplierService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<string, EffectiveMultiplierResult>> ResolveForRegionsAsync(
        IEnumerable<EffectiveMultiplierInput> regions,
        PricingIndexType indexType,
        Guid? userId,
        string? planType = null)
    {
        var normalizedInputs = regions
            .Select(r =>
            {
                var normalized = RegionCodeNormalizer.NormalizeToAlpha3(r.RegionCode);
                return new
                {
                    OriginalRegionCode = r.RegionCode,
                    NormalizedRegionCode = normalized,
                    CurrencyCode = string.IsNullOrWhiteSpace(r.CurrencyCode)
                        ? null
                        : r.CurrencyCode.Trim().ToUpperInvariant()
                };
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.NormalizedRegionCode))
            .GroupBy(r => r.NormalizedRegionCode!)
            .Select(g => g.First())
            .ToList();

        var requestedRegions = normalizedInputs
            .Select(r => r.NormalizedRegionCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var query = _context.PppMultipliers
            .Where(m => requestedRegions.Contains(m.RegionCode) || m.CurrencyCode != null)
            .Where(m => m.IndexType == indexType)
            .Where(m => m.UserId == null || m.UserId == userId);

        if (indexType == PricingIndexType.Netflix)
        {
            var normalizedPlanType = NormalizePlanType(planType);
            query = query.Where(m => m.PlanType == normalizedPlanType);
        }
        else
        {
            query = query.Where(m => m.PlanType == null);
        }

        var rows = await query.ToListAsync();

        var preferredRowsByRegion = rows
            .GroupBy(m => m.RegionCode)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(m => m.UserId == userId && m.UserId != null)
                    .ThenByDescending(m => m.UserId != null)
                    .ThenByDescending(m => m.UpdatedAt)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var currencyFallback = rows
            .Where(m => !string.IsNullOrWhiteSpace(m.CurrencyCode))
            .GroupBy(m => m.CurrencyCode!.ToUpperInvariant())
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var ordered = g.Select(x => x.Multiplier).OrderBy(v => v).ToList();
                    if (ordered.Count == 0)
                    {
                        return (Median: 1.0m, Count: 0);
                    }

                    var middle = ordered.Count / 2;
                    var median = ordered.Count % 2 == 1
                        ? ordered[middle]
                        : (ordered[middle - 1] + ordered[middle]) / 2m;

                    return (Median: median, Count: ordered.Count);
                },
                StringComparer.OrdinalIgnoreCase);

        var results = new Dictionary<string, EffectiveMultiplierResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in normalizedInputs)
        {
            var region = input.NormalizedRegionCode!;
            if (preferredRowsByRegion.TryGetValue(region, out var direct))
            {
                results[region] = new EffectiveMultiplierResult
                {
                    RegionCode = region,
                    Multiplier = direct.Multiplier,
                    IsFallback = false,
                    Source = direct.Source,
                    SourceRegionCount = 1
                };
                continue;
            }

            if (!string.IsNullOrWhiteSpace(input.CurrencyCode) &&
                currencyFallback.TryGetValue(input.CurrencyCode!, out var fallback))
            {
                results[region] = new EffectiveMultiplierResult
                {
                    RegionCode = region,
                    Multiplier = fallback.Median,
                    IsFallback = true,
                    FallbackReason = $"currency_median:{input.CurrencyCode}",
                    Source = "currency_fallback",
                    SourceRegionCount = fallback.Count
                };
                continue;
            }

            results[region] = new EffectiveMultiplierResult
            {
                RegionCode = region,
                Multiplier = 1.0m,
                IsFallback = true,
                FallbackReason = "default_1_0",
                Source = "default",
                SourceRegionCount = 0
            };
        }

        return results;
    }

    private static string? NormalizePlanType(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType))
        {
            return "standard";
        }

        return planType.Trim().ToLowerInvariant();
    }
}
