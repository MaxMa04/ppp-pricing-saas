using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.Domain.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/app-store")]
public class AppStoreController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AppStoreController> _logger;
    private readonly HttpClient _httpClient;

    public AppStoreController(
        ApplicationDbContext context,
        ILogger<AppStoreController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    private async Task<Guid?> GetUserIdAsync()
    {
        var firebaseUid = HttpContext.Items["FirebaseUid"]?.ToString();
        if (string.IsNullOrEmpty(firebaseUid)) return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        return user?.Id;
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] AppStoreConnectRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrEmpty(request.KeyId) ||
            string.IsNullOrEmpty(request.IssuerId) ||
            string.IsNullOrEmpty(request.PrivateKey))
        {
            return BadRequest(new { error = "Missing required fields: keyId, issuerId, privateKey" });
        }

        // Validate the credentials by generating a token and making a test API call
        try
        {
            var token = GenerateJwtToken(request.KeyId, request.IssuerId, request.PrivateKey);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await _httpClient.GetAsync("https://api.appstoreconnect.apple.com/v1/apps?limit=1");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("App Store Connect validation failed: {Error}", errorContent);
                return BadRequest(new { error = "Invalid App Store Connect credentials" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate App Store Connect credentials");
            return BadRequest(new { error = "Invalid private key format" });
        }

        // Store connection (private key should be encrypted in production)
        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.AppStore);

        if (connection == null)
        {
            connection = new StoreConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                StoreType = StoreType.AppStore,
                CreatedAt = DateTime.UtcNow
            };
            _context.StoreConnections.Add(connection);
        }

        connection.AppleKeyId = request.KeyId;
        connection.AppleIssuerId = request.IssuerId;
        // In production, encrypt this!
        connection.ApplePrivateKeyEncrypted = System.Text.Encoding.UTF8.GetBytes(request.PrivateKey);
        connection.IsActive = true;
        connection.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} connected App Store account", userId);

        return Ok(new { success = true, connectionId = connection.Id });
    }

    [HttpGet("apps")]
    public async Task<IActionResult> GetApps()
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.AppStore && sc.IsActive);

        if (connection == null || connection.ApplePrivateKeyEncrypted == null)
        {
            return BadRequest(new { error = "App Store not connected" });
        }

        // Return apps from database (synced separately)
        var apps = await _context.Apps
            .Where(a => a.StoreConnectionId == connection.Id)
            .Select(a => new
            {
                a.Id,
                a.AppName,
                a.BundleId,
                a.AppStoreId,
                a.IconUrl,
                a.CreatedAt,
                SubscriptionCount = a.Subscriptions.Count
            })
            .ToListAsync();

        return Ok(apps);
    }

    [HttpPost("apps/{appStoreId}/sync")]
    public async Task<IActionResult> SyncApp(string appStoreId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.AppStore && sc.IsActive);

        if (connection == null || connection.ApplePrivateKeyEncrypted == null)
        {
            return BadRequest(new { error = "App Store not connected" });
        }

        // In a real implementation, this would call the App Store Connect API
        // For now, we'll just create a placeholder app

        var existingApp = await _context.Apps
            .FirstOrDefaultAsync(a => a.AppStoreId == appStoreId && a.UserId == userId);

        if (existingApp == null)
        {
            existingApp = new App
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                StoreConnectionId = connection.Id,
                StoreType = StoreType.AppStore,
                AppStoreId = appStoreId,
                AppName = $"App {appStoreId}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Apps.Add(existingApp);
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            existingApp.Id,
            existingApp.AppName,
            existingApp.AppStoreId,
            message = "App synced successfully"
        });
    }

    private string GenerateJwtToken(string keyId, string issuerId, string privateKeyPem)
    {
        var now = DateTimeOffset.UtcNow;

        // Parse the EC private key
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);

        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var header = new JwtHeader(credentials);
        header["kid"] = keyId;

        var payload = new JwtPayload
        {
            { "iss", issuerId },
            { "iat", now.ToUnixTimeSeconds() },
            { "exp", now.AddMinutes(19).ToUnixTimeSeconds() },
            { "aud", "appstoreconnect-v1" }
        };

        var token = new JwtSecurityToken(header, payload);
        var handler = new JwtSecurityTokenHandler();

        return handler.WriteToken(token);
    }
}

public class AppStoreConnectRequest
{
    public string KeyId { get; set; } = string.Empty;
    public string IssuerId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
}
