namespace PppPricing.Domain.Models;

public class PppMultiplier
{
    public Guid Id { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public string? CountryName { get; set; }
    public decimal Multiplier { get; set; }  // e.g., 0.45 for 45% of base price
    public string? Source { get; set; }       // 'big_mac_index', 'world_bank', 'custom'
    public string? CurrencyCode { get; set; }
    public PricingIndexType IndexType { get; set; } = PricingIndexType.BigMac;
    public string? PlanType { get; set; }      // netflix plan: mobile/basic/standard/premium
    public DateTime DataDate { get; set; }    // When the source data was published
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // User-specific custom multipliers (null = system-wide multiplier)
    public Guid? UserId { get; set; }
    public User? User { get; set; }
}
