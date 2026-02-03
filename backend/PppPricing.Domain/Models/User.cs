namespace PppPricing.Domain.Models;

public class User
{
    public Guid Id { get; set; }
    public string FirebaseUid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<StoreConnection> StoreConnections { get; set; } = new List<StoreConnection>();
    public ICollection<App> Apps { get; set; } = new List<App>();
    public ICollection<PriceChange> PriceChanges { get; set; } = new List<PriceChange>();
}
