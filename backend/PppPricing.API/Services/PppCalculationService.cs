using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.Domain.Models;

namespace PppPricing.API.Services;

public interface IPppCalculationService
{
    Task<decimal> CalculatePppPrice(decimal basePrice, string baseCurrency, string targetRegion);
    Task<List<RegionalPrice>> CalculateAllRegionalPrices(decimal basePrice, string baseCurrency);
    decimal FindClosestApplePricePoint(decimal targetPrice, List<decimal> availablePricePoints);
    Task<PriceChangePreview> PreviewPriceChanges(Guid subscriptionId);
}

public class PppCalculationService : IPppCalculationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PppCalculationService> _logger;

    // Common currency to region mapping
    private static readonly Dictionary<string, string> RegionToCurrency = new()
    {
        { "US", "USD" }, { "CA", "CAD" }, { "GB", "GBP" }, { "DE", "EUR" },
        { "FR", "EUR" }, { "IT", "EUR" }, { "ES", "EUR" }, { "NL", "EUR" },
        { "BE", "EUR" }, { "AT", "EUR" }, { "IE", "EUR" }, { "PT", "EUR" },
        { "FI", "EUR" }, { "GR", "EUR" }, { "JP", "JPY" }, { "AU", "AUD" },
        { "KR", "KRW" }, { "CN", "CNY" }, { "IN", "INR" }, { "BR", "BRL" },
        { "MX", "MXN" }, { "RU", "RUB" }, { "ZA", "ZAR" }, { "TR", "TRY" },
        { "PL", "PLN" }, { "SE", "SEK" }, { "NO", "NOK" }, { "DK", "DKK" },
        { "CH", "CHF" }, { "NZ", "NZD" }, { "SG", "SGD" }, { "HK", "HKD" },
        { "TW", "TWD" }, { "TH", "THB" }, { "ID", "IDR" }, { "MY", "MYR" },
        { "PH", "PHP" }, { "VN", "VND" }, { "AE", "AED" }, { "SA", "SAR" },
        { "IL", "ILS" }, { "EG", "EGP" }, { "NG", "NGN" }, { "KE", "KES" },
        { "CL", "CLP" }, { "CO", "COP" }, { "AR", "ARS" }, { "PE", "PEN" },
    };

    public PppCalculationService(ApplicationDbContext context, ILogger<PppCalculationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<decimal> CalculatePppPrice(decimal basePrice, string baseCurrency, string targetRegion)
    {
        var normalizedRegion = RegionCodeNormalizer.NormalizeToAlpha3(targetRegion) ?? targetRegion.ToUpperInvariant();

        // Get PPP multiplier for target region
        var multiplier = await _context.PppMultipliers
            .Where(m => m.RegionCode == normalizedRegion &&
                        m.IndexType == PricingIndexType.BigMac &&
                        m.PlanType == null)
            .Select(m => m.Multiplier)
            .FirstOrDefaultAsync();

        if (multiplier == 0) multiplier = 1.0m; // Default to no adjustment

        // Get target currency
        var targetCurrency = GetCurrencyForRegion(targetRegion);

        // Convert to target currency
        var exchangeRate = GetExchangeRate(baseCurrency, targetCurrency);

        // Calculate: BasePrice * ExchangeRate * PPP_Multiplier
        var pppPrice = basePrice * exchangeRate * multiplier;

        // Round to local convention
        return RoundToLocalConvention(pppPrice, targetCurrency);
    }

    public async Task<List<RegionalPrice>> CalculateAllRegionalPrices(decimal basePrice, string baseCurrency)
    {
        var multipliers = await _context.PppMultipliers.ToListAsync();
        var prices = new List<RegionalPrice>();

        foreach (var region in RegionToCurrency.Keys)
        {
            var targetCurrency = GetCurrencyForRegion(region);
            var exchangeRate = GetExchangeRate(baseCurrency, targetCurrency);
            var multiplier = multipliers.FirstOrDefault(m => m.RegionCode == region)?.Multiplier ?? 1.0m;

            var originalPrice = basePrice * exchangeRate;
            var pppPrice = originalPrice * multiplier;

            prices.Add(new RegionalPrice
            {
                RegionCode = region,
                CurrencyCode = targetCurrency,
                OriginalPrice = RoundToLocalConvention(originalPrice, targetCurrency),
                PppAdjustedPrice = RoundToLocalConvention(pppPrice, targetCurrency),
                Multiplier = multiplier
            });
        }

        return prices;
    }

    public decimal FindClosestApplePricePoint(decimal targetPrice, List<decimal> availablePricePoints)
    {
        if (!availablePricePoints.Any()) return targetPrice;

        return availablePricePoints
            .OrderBy(p => Math.Abs(p - targetPrice))
            .First();
    }

    public async Task<PriceChangePreview> PreviewPriceChanges(Guid subscriptionId)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Prices)
            .Include(s => s.App)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (subscription == null)
        {
            throw new ArgumentException("Subscription not found");
        }

        var multipliers = await _context.PppMultipliers.ToDictionaryAsync(m => m.RegionCode, m => m.Multiplier);

        var previews = new List<PricePreviewItem>();
        var increases = 0;
        var decreases = 0;
        var unchanged = 0;

        foreach (var price in subscription.Prices)
        {
            var multiplier = multipliers.GetValueOrDefault(price.RegionCode, 1.0m);
            var suggestedPrice = price.CurrentPrice.HasValue
                ? Math.Round(price.CurrentPrice.Value * multiplier, 2)
                : (decimal?)null;

            var change = price.CurrentPrice.HasValue && suggestedPrice.HasValue
                ? (suggestedPrice.Value - price.CurrentPrice.Value) / price.CurrentPrice.Value * 100
                : (decimal?)null;

            if (change > 1) increases++;
            else if (change < -1) decreases++;
            else unchanged++;

            previews.Add(new PricePreviewItem
            {
                RegionCode = price.RegionCode,
                CurrencyCode = price.CurrencyCode,
                CurrentPrice = price.CurrentPrice,
                SuggestedPrice = suggestedPrice,
                Multiplier = multiplier,
                PercentageChange = change
            });
        }

        return new PriceChangePreview
        {
            SubscriptionId = subscriptionId,
            SubscriptionName = subscription.Name ?? subscription.ProductId,
            Increases = increases,
            Decreases = decreases,
            Unchanged = unchanged,
            Total = previews.Count,
            Prices = previews
        };
    }

    private string GetCurrencyForRegion(string regionCode)
    {
        var alpha2 = RegionCodeNormalizer.NormalizeToAlpha2(regionCode) ?? regionCode.ToUpperInvariant();
        return RegionToCurrency.GetValueOrDefault(alpha2, "USD");
    }

    private decimal GetExchangeRate(string fromCurrency, string toCurrency)
    {
        if (fromCurrency == toCurrency) return 1.0m;
        return CurrencyConversion.Convert(1m, fromCurrency, toCurrency);
    }

    private decimal RoundToLocalConvention(decimal price, string currency)
    {
        // Round to appropriate decimal places based on currency
        return currency switch
        {
            "JPY" or "KRW" or "VND" or "IDR" => Math.Round(price, 0),
            "TWD" or "CLP" or "COP" or "ARS" => Math.Round(price, 0),
            _ => Math.Round(price, 2)
        };
    }
}

public class RegionalPrice
{
    public string RegionCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal PppAdjustedPrice { get; set; }
    public decimal Multiplier { get; set; }
    public decimal? ApplePricePoint { get; set; }
    public decimal? GooglePrice { get; set; }
}

public class PriceChangePreview
{
    public Guid SubscriptionId { get; set; }
    public string SubscriptionName { get; set; } = string.Empty;
    public int Increases { get; set; }
    public int Decreases { get; set; }
    public int Unchanged { get; set; }
    public int Total { get; set; }
    public List<PricePreviewItem> Prices { get; set; } = new();
}

public class PricePreviewItem
{
    public string RegionCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal? CurrentPrice { get; set; }
    public decimal? SuggestedPrice { get; set; }
    public decimal Multiplier { get; set; }
    public decimal? PercentageChange { get; set; }
}
