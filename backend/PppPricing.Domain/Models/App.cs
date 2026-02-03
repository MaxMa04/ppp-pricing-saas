namespace PppPricing.Domain.Models;

public class App
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid StoreConnectionId { get; set; }
    public StoreType StoreType { get; set; }

    // Identifiers
    public string? PackageName { get; set; }  // Google: com.example.app
    public string? BundleId { get; set; }     // Apple: com.example.app
    public string? AppStoreId { get; set; }   // Apple: numeric ID

    public string AppName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public StoreConnection StoreConnection { get; set; } = null!;
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
