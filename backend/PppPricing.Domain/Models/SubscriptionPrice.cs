namespace PppPricing.Domain.Models;

public class SubscriptionPrice
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }

    public string RegionCode { get; set; } = string.Empty;   // ISO 3166-1 alpha-2/3
    public string CurrencyCode { get; set; } = string.Empty;

    public decimal? CurrentPrice { get; set; }
    public decimal? PppSuggestedPrice { get; set; }
    public decimal? PppMultiplier { get; set; }

    public DateTime? LastSyncedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }

    // Navigation properties
    public Subscription Subscription { get; set; } = null!;
}
