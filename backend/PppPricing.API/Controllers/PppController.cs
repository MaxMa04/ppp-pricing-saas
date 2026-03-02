using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.API.Services;
using PppPricing.Domain.Models;
using System.ComponentModel.DataAnnotations;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PppController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPricingIndexImportService _importService;
    private readonly IEffectiveMultiplierService _effectiveMultiplierService;
    private readonly ILogger<PppController> _logger;

    public PppController(
        ApplicationDbContext context,
        IPricingIndexImportService importService,
        IEffectiveMultiplierService effectiveMultiplierService,
        ILogger<PppController> logger)
    {
        _context = context;
        _importService = importService;
        _effectiveMultiplierService = effectiveMultiplierService;
        _logger = logger;
    }

    private async Task<User?> GetUserAsync()
    {
        var firebaseUid = HttpContext.Items["FirebaseUid"]?.ToString();
        if (string.IsNullOrEmpty(firebaseUid)) return null;

        return await _context.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
    }

    private async Task<Guid?> GetUserIdAsync()
    {
        var user = await GetUserAsync();
        return user?.Id;
    }

    [HttpGet("multipliers")]
    public async Task<IActionResult> GetMultipliers(
        [FromQuery] PricingIndexType? indexType = null,
        [FromQuery] string? planType = null)
    {
        var userId = await GetUserIdAsync();

        // Return system multipliers (UserId == null) plus user's custom multipliers
        var query = _context.PppMultipliers
            .Where(m => m.UserId == null || m.UserId == userId);

        if (indexType.HasValue)
        {
            query = query.Where(m => m.IndexType == indexType.Value);
        }

        if (indexType == PricingIndexType.Netflix)
        {
            if (!string.IsNullOrWhiteSpace(planType))
            {
                var normalizedPlanType = planType.Trim().ToLowerInvariant();
                query = query.Where(m => m.PlanType == normalizedPlanType);
            }
        }
        else
        {
            query = query.Where(m => m.PlanType == null);
        }

        var multipliers = await query.ToListAsync();

        var deduplicated = multipliers
            .GroupBy(m => new
            {
                RegionCode = m.RegionCode.ToUpperInvariant(),
                m.IndexType,
                PlanType = m.PlanType?.ToLowerInvariant()
            })
            .Select(g => g
                .OrderByDescending(m => m.UserId == userId && m.UserId != null)
                .ThenByDescending(m => m.UserId != null)
                .ThenByDescending(m => m.UpdatedAt)
                .First())
            .OrderBy(m => m.CountryName)
            .Select(m => new
            {
                m.Id,
                m.RegionCode,
                m.CountryName,
                m.Multiplier,
                m.Source,
                m.CurrencyCode,
                m.PlanType,
                IndexType = m.IndexType.ToString(),
                m.DataDate,
                m.UpdatedAt,
                IsCustom = m.UserId != null
            })
            .ToList();

        return Ok(deduplicated);
    }

    [HttpGet("multipliers/{regionCode}")]
    public async Task<IActionResult> GetMultiplier(
        string regionCode,
        [FromQuery] PricingIndexType? indexType = null,
        [FromQuery] string? planType = null)
    {
        var userId = await GetUserIdAsync();
        var normalizedRegionCode = RegionCodeNormalizer.NormalizeToAlpha3(regionCode) ?? regionCode.Trim().ToUpperInvariant();

        // First try to find user's custom multiplier, then fall back to system multiplier
        var query = _context.PppMultipliers
            .Where(m => m.RegionCode == normalizedRegionCode && (m.UserId == userId || m.UserId == null));

        if (indexType.HasValue)
        {
            query = query.Where(m => m.IndexType == indexType.Value);
        }

        if (indexType == PricingIndexType.Netflix)
        {
            var normalizedPlanType = string.IsNullOrWhiteSpace(planType) ? "standard" : planType.Trim().ToLowerInvariant();
            query = query.Where(m => m.PlanType == normalizedPlanType);
        }
        else
        {
            query = query.Where(m => m.PlanType == null);
        }

        var multiplier = await query
            .OrderByDescending(m => m.UserId == userId && m.UserId != null)
            .ThenByDescending(m => m.UserId != null)
            .ThenByDescending(m => m.UpdatedAt)
            .FirstOrDefaultAsync();

        if (multiplier == null) return NotFound();

        return Ok(new
        {
            multiplier.Id,
            multiplier.RegionCode,
            multiplier.CountryName,
            multiplier.Multiplier,
            multiplier.Source,
            multiplier.CurrencyCode,
            multiplier.PlanType,
            IndexType = multiplier.IndexType.ToString(),
            multiplier.DataDate,
            multiplier.UpdatedAt,
            IsCustom = multiplier.UserId != null
        });
    }

    [HttpPut("multipliers/{regionCode}")]
    public async Task<IActionResult> UpdateMultiplier(string regionCode, [FromBody] UpdateMultiplierRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await GetUserAsync();
        if (user == null) return Unauthorized();
        var normalizedRegionCode = RegionCodeNormalizer.NormalizeToAlpha3(regionCode) ?? regionCode.Trim().ToUpperInvariant();
        var normalizedPlanType = NormalizePlanType(request.PlanType);

        var multiplier = await _context.PppMultipliers
            .FirstOrDefaultAsync(m =>
                m.RegionCode == normalizedRegionCode &&
                m.IndexType == request.IndexType &&
                m.PlanType == normalizedPlanType &&
                m.UserId == null);

        if (multiplier != null)
        {
            // System multiplier exists - only admins can modify
            if (user.Role != UserRole.Admin)
            {
                return Forbid();
            }

            multiplier.Multiplier = request.Multiplier;
            if (request.CountryName != null) multiplier.CountryName = request.CountryName;
            if (request.Source != null) multiplier.Source = request.Source;
            if (request.CurrencyCode != null) multiplier.CurrencyCode = request.CurrencyCode.ToUpperInvariant();
            multiplier.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // No system multiplier - create as system multiplier (admin only)
            if (user.Role != UserRole.Admin)
            {
                return Forbid();
            }

            multiplier = new PppMultiplier
            {
                Id = Guid.NewGuid(),
                RegionCode = normalizedRegionCode,
                CountryName = request.CountryName,
                Multiplier = request.Multiplier,
                Source = request.Source ?? "custom",
                CurrencyCode = request.CurrencyCode?.ToUpperInvariant(),
                IndexType = request.IndexType,
                PlanType = normalizedPlanType,
                UserId = null, // System multiplier
                DataDate = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.PppMultipliers.Add(multiplier);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Admin {UserId} updated system multiplier for {RegionCode}", user.Id, regionCode);

        return Ok(new
        {
            multiplier.Id,
            multiplier.RegionCode,
            multiplier.CountryName,
            multiplier.Multiplier,
            multiplier.Source,
            multiplier.UpdatedAt,
            IsCustom = false
        });
    }

    [HttpPost("multipliers/custom")]
    public async Task<IActionResult> CreateCustomMultiplier([FromBody] CreateCustomMultiplierRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await GetUserAsync();
        if (user == null) return Unauthorized();
        var normalizedRegionCode = RegionCodeNormalizer.NormalizeToAlpha3(request.RegionCode) ?? request.RegionCode.Trim().ToUpperInvariant();
        var normalizedPlanType = NormalizePlanType(request.PlanType);

        // Check if user already has a custom multiplier for this region
        var existing = await _context.PppMultipliers
            .FirstOrDefaultAsync(m =>
                m.RegionCode == normalizedRegionCode &&
                m.IndexType == request.IndexType &&
                m.PlanType == normalizedPlanType &&
                m.UserId == user.Id);

        if (existing != null)
        {
            existing.Multiplier = request.Multiplier;
            if (request.CountryName != null) existing.CountryName = request.CountryName;
            existing.Source = "custom";
            existing.CurrencyCode = request.CurrencyCode?.ToUpperInvariant();
            existing.IndexType = request.IndexType;
            existing.PlanType = normalizedPlanType;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new PppMultiplier
            {
                Id = Guid.NewGuid(),
                RegionCode = normalizedRegionCode,
                CountryName = request.CountryName,
                Multiplier = request.Multiplier,
                Source = "custom",
                CurrencyCode = request.CurrencyCode?.ToUpperInvariant(),
                IndexType = request.IndexType,
                PlanType = normalizedPlanType,
                UserId = user.Id,
                DataDate = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.PppMultipliers.Add(existing);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} created/updated custom multiplier for {RegionCode}", user.Id, request.RegionCode);

        return Ok(new
        {
            existing.Id,
            existing.RegionCode,
            existing.CountryName,
            existing.Multiplier,
            existing.Source,
            existing.UpdatedAt,
            IsCustom = true
        });
    }

    [HttpDelete("multipliers/custom/{regionCode}")]
    public async Task<IActionResult> DeleteCustomMultiplier(
        string regionCode,
        [FromQuery] PricingIndexType indexType = PricingIndexType.BigMac,
        [FromQuery] string? planType = null)
    {
        var user = await GetUserAsync();
        if (user == null) return Unauthorized();
        var normalizedRegionCode = RegionCodeNormalizer.NormalizeToAlpha3(regionCode) ?? regionCode.Trim().ToUpperInvariant();
        var normalizedPlanType = NormalizePlanType(planType);

        var multiplier = await _context.PppMultipliers
            .FirstOrDefaultAsync(m =>
                m.RegionCode == normalizedRegionCode &&
                m.IndexType == indexType &&
                m.PlanType == normalizedPlanType &&
                m.UserId == user.Id);

        if (multiplier == null)
        {
            return NotFound(new { error = "Custom multiplier not found" });
        }

        _context.PppMultipliers.Remove(multiplier);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted custom multiplier for {RegionCode}", user.Id, regionCode);

        return NoContent();
    }

    [HttpPost("multipliers/import")]
    public async Task<IActionResult> ImportMultipliers([FromBody] List<ImportMultiplierRequest> requests)
    {
        var user = await GetUserAsync();
        if (user == null) return Unauthorized();

        var imported = 0;
        var updated = 0;

        foreach (var request in requests)
        {
            // Validate each multiplier value
            if (request.Multiplier < 0.01m || request.Multiplier > 10.0m)
            {
                continue; // Skip invalid entries
            }

            var normalizedRegionCode = RegionCodeNormalizer.NormalizeToAlpha3(request.RegionCode) ?? request.RegionCode.Trim().ToUpperInvariant();
            var normalizedPlanType = NormalizePlanType(request.PlanType);
            var existing = await _context.PppMultipliers
                .FirstOrDefaultAsync(m =>
                    m.RegionCode == normalizedRegionCode &&
                    m.IndexType == request.IndexType &&
                    m.PlanType == normalizedPlanType &&
                    m.UserId == null);

            if (existing == null)
            {
                _context.PppMultipliers.Add(new PppMultiplier
                {
                    Id = Guid.NewGuid(),
                    RegionCode = normalizedRegionCode,
                    CountryName = request.CountryName,
                    Multiplier = request.Multiplier,
                    Source = request.Source ?? "import",
                    CurrencyCode = request.CurrencyCode?.ToUpperInvariant(),
                    IndexType = request.IndexType,
                    PlanType = normalizedPlanType,
                    UserId = null,
                    DataDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                imported++;
            }
            else
            {
                existing.Multiplier = request.Multiplier;
                if (request.CountryName != null) existing.CountryName = request.CountryName;
                if (request.Source != null) existing.Source = request.Source;
                if (request.CurrencyCode != null) existing.CurrencyCode = request.CurrencyCode.ToUpperInvariant();
                existing.IndexType = request.IndexType;
                existing.PlanType = normalizedPlanType;
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} imported {Total} multipliers", user.Id, imported + updated);

        return Ok(new { imported, updated, total = imported + updated });
    }

    [HttpPost("import/bigmac")]
    public async Task<IActionResult> ImportBigMacIndex()
    {
        var user = await GetUserAsync();
        if (user == null) return Unauthorized();

        var result = await _importService.ImportBigMacIndexAsync();

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        _logger.LogInformation("User {UserId} imported Big Mac Index: {Total} entries", user.Id, result.Total);

        return Ok(result);
    }

    [HttpPost("import/netflix")]
    public async Task<IActionResult> ImportNetflixIndex([FromQuery] string? planType = null)
    {
        var user = await GetUserAsync();
        if (user == null) return Unauthorized();

        var result = await _importService.ImportNetflixIndexAsync(planType);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        _logger.LogInformation("User {UserId} imported Netflix Index ({PlanType}): {Total} entries", user.Id, planType ?? "all", result.Total);

        return Ok(result);
    }

    [HttpPost("import/wages")]
    public async Task<IActionResult> ImportWageData()
    {
        var user = await GetUserAsync();
        if (user == null) return Unauthorized();

        var result = await _importService.ImportWageDataAsync();

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        _logger.LogInformation("User {UserId} imported wage data: {Total} entries", user.Id, result.Total);

        return Ok(result);
    }

    [HttpPost("import/working-hours")]
    public async Task<IActionResult> CalculateWorkingHours()
    {
        var user = await GetUserAsync();
        if (user == null) return Unauthorized();

        var result = await _importService.CalculateBigMacWorkingHoursAsync();

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        _logger.LogInformation("User {UserId} calculated Big Mac Working Hours: {Total} entries", user.Id, result.Total);

        return Ok(result);
    }

    [HttpGet("effective-multipliers")]
    public async Task<IActionResult> GetEffectiveMultipliers(
        [FromQuery] PricingIndexType indexType,
        [FromQuery] string? planType = null,
        [FromQuery] Guid? appId = null,
        [FromQuery] string? regionCodes = null)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var regionInputs = new List<EffectiveMultiplierInput>();

        if (appId.HasValue)
        {
            var appExists = await _context.Apps.AnyAsync(a => a.Id == appId.Value && a.UserId == userId);
            if (!appExists)
            {
                return NotFound(new { error = "App not found" });
            }

            var appRegions = await _context.SubscriptionPrices
                .Where(p => p.Subscription.AppId == appId.Value)
                .Select(p => new { p.RegionCode, p.CurrencyCode })
                .ToListAsync();

            regionInputs.AddRange(appRegions.Select(r => new EffectiveMultiplierInput(r.RegionCode, r.CurrencyCode)));
        }

        if (!string.IsNullOrWhiteSpace(regionCodes))
        {
            regionInputs.AddRange(regionCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(code => new EffectiveMultiplierInput(code, null)));
        }

        if (regionInputs.Count == 0)
        {
            return BadRequest(new { error = "Provide appId or regionCodes query parameter." });
        }

        var effective = await _effectiveMultiplierService.ResolveForRegionsAsync(
            regionInputs,
            indexType,
            userId,
            planType);

        return Ok(effective.Values
            .OrderBy(v => v.RegionCode)
            .Select(v => new
            {
                v.RegionCode,
                v.Multiplier,
                v.IsFallback,
                v.FallbackReason,
                v.Source,
                v.SourceRegionCount
            }));
    }

    [HttpGet("raw-data/{indexType}")]
    public async Task<IActionResult> GetRawData(PricingIndexType indexType)
    {
        var rawData = await _context.PricingIndexRawData
            .Where(r => r.IndexType == indexType)
            .OrderBy(r => r.CountryName)
            .Select(r => new
            {
                r.Id,
                r.RegionCode,
                r.CountryName,
                r.CurrencyCode,
                r.LocalPrice,
                r.UsdPrice,
                r.ExchangeRate,
                r.HourlyWage,
                r.WorkingHours,
                r.PlanType,
                r.DataDate,
                r.ImportedAt
            })
            .ToListAsync();

        return Ok(rawData);
    }

    [HttpGet("index-types")]
    public IActionResult GetIndexTypes()
    {
        var indexTypes = Enum.GetValues<PricingIndexType>()
            .Select(t => new
            {
                Value = (int)t,
                Name = t.ToString(),
                DisplayName = t switch
                {
                    PricingIndexType.BigMac => "Big Mac Index",
                    PricingIndexType.Netflix => "Netflix Index",
                    PricingIndexType.BigMacWorkingHours => "Big Mac Working Hours",
                    _ => t.ToString()
                }
            })
            .ToList();

        return Ok(indexTypes);
    }

    private static string? NormalizePlanType(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType))
        {
            return null;
        }

        return planType.Trim().ToLowerInvariant();
    }
}

public class UpdateMultiplierRequest
{
    [Required]
    [Range(0.01, 10.0, ErrorMessage = "Multiplier must be between 0.01 and 10.0")]
    public decimal Multiplier { get; set; }

    public string? CountryName { get; set; }
    public string? Source { get; set; }
    public string? CurrencyCode { get; set; }
    public PricingIndexType IndexType { get; set; } = PricingIndexType.BigMac;
    public string? PlanType { get; set; }
}

public class CreateCustomMultiplierRequest
{
    [Required]
    [StringLength(10, MinimumLength = 2)]
    public string RegionCode { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 10.0, ErrorMessage = "Multiplier must be between 0.01 and 10.0")]
    public decimal Multiplier { get; set; }

    public string? CountryName { get; set; }
    public string? CurrencyCode { get; set; }
    public PricingIndexType IndexType { get; set; } = PricingIndexType.BigMac;
    public string? PlanType { get; set; }
}

public class ImportMultiplierRequest
{
    [Required]
    public string RegionCode { get; set; } = string.Empty;

    [Range(0.01, 10.0, ErrorMessage = "Multiplier must be between 0.01 and 10.0")]
    public decimal Multiplier { get; set; }

    public string? CountryName { get; set; }
    public string? Source { get; set; }
    public string? CurrencyCode { get; set; }
    public PricingIndexType IndexType { get; set; } = PricingIndexType.BigMac;
    public string? PlanType { get; set; }
}
