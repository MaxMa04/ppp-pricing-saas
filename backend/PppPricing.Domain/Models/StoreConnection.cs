namespace PppPricing.Domain.Models;

public class StoreConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public StoreType StoreType { get; set; }

    // Google Play OAuth tokens (encrypted)
    public byte[]? GoogleAccessTokenEncrypted { get; set; }
    public byte[]? GoogleRefreshTokenEncrypted { get; set; }
    public DateTime? GoogleTokenExpiry { get; set; }

    // App Store API Key details (encrypted)
    public string? AppleKeyId { get; set; }
    public string? AppleIssuerId { get; set; }
    public byte[]? ApplePrivateKeyEncrypted { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<App> Apps { get; set; } = new List<App>();
}

public enum StoreType
{
    GooglePlay,
    AppStore
}
