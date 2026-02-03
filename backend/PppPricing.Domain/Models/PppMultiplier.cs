namespace PppPricing.Domain.Models;

public class PppMultiplier
{
    public Guid Id { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public string? CountryName { get; set; }
    public decimal Multiplier { get; set; }  // e.g., 0.45 for 45% of base price
    public string? Source { get; set; }       // 'big_mac_index', 'world_bank', 'custom'
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
