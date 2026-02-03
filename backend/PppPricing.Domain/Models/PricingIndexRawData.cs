namespace PppPricing.Domain.Models;

public class PricingIndexRawData
{
    public Guid Id { get; set; }
    public PricingIndexType IndexType { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public string? CountryName { get; set; }
    public string? CurrencyCode { get; set; }

    // Price data
    public decimal LocalPrice { get; set; }      // e.g., Big Mac price in local currency
    public decimal UsdPrice { get; set; }        // Price in USD
    public decimal? ExchangeRate { get; set; }   // Local currency to USD

    // Working hours calculation data
    public decimal? HourlyWage { get; set; }     // Average hourly wage in local currency
    public decimal? WorkingHours { get; set; }   // Hours to buy item (LocalPrice / HourlyWage)

    // Netflix specific
    public string? PlanType { get; set; }        // 'mobile', 'basic', 'standard', 'premium'

    // Metadata
    public DateTime DataDate { get; set; }       // When the source data was published
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
