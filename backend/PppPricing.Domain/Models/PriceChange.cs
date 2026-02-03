namespace PppPricing.Domain.Models;

public class PriceChange
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid? UserId { get; set; }

    public string RegionCode { get; set; } = string.Empty;
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public string? CurrencyCode { get; set; }

    public PriceChangeType ChangeType { get; set; }
    public PriceChangeStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedAt { get; set; }

    // Navigation properties
    public Subscription Subscription { get; set; } = null!;
    public User? User { get; set; }
}

public enum PriceChangeType
{
    PppAdjustment,
    Manual,
    Sync
}

public enum PriceChangeStatus
{
    Pending,
    Applied,
    Failed
}
