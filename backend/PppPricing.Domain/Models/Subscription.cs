namespace PppPricing.Domain.Models;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid AppId { get; set; }

    // Store identifiers
    public string ProductId { get; set; } = string.Empty;  // Google: subscription ID, Apple: product ID
    public string? BasePlanId { get; set; }                 // Google only

    public string? Name { get; set; }
    public string? BillingPeriod { get; set; }  // 'monthly', 'yearly', etc.

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public App App { get; set; } = null!;
    public ICollection<SubscriptionPrice> Prices { get; set; } = new List<SubscriptionPrice>();
    public ICollection<PriceChange> PriceChanges { get; set; } = new List<PriceChange>();
}
