using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.Domain.Models;
using System.Text.Json;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/google-play")]
public class GooglePlayController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GooglePlayController> _logger;
    private readonly HttpClient _httpClient;

    public GooglePlayController(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<GooglePlayController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
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

    [HttpGet("auth/url")]
    public IActionResult GetAuthUrl()
    {
        var clientId = _configuration["Google:ClientId"];
        var redirectUri = _configuration["Google:RedirectUri"] ?? $"{Request.Scheme}://{Request.Host}/api/google-play/auth/callback";

        if (string.IsNullOrEmpty(clientId))
        {
            return BadRequest(new { error = "Google OAuth not configured" });
        }

        var scope = "https://www.googleapis.com/auth/androidpublisher";
        var state = Guid.NewGuid().ToString();

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
            $"client_id={Uri.EscapeDataString(clientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(scope)}&" +
            $"access_type=offline&" +
            $"prompt=consent&" +
            $"state={state}";

        return Ok(new { url = authUrl, state });
    }

    [HttpPost("auth/callback")]
    public async Task<IActionResult> HandleCallback([FromBody] OAuthCallbackRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var clientId = _configuration["Google:ClientId"];
        var clientSecret = _configuration["Google:ClientSecret"];
        var redirectUri = _configuration["Google:RedirectUri"] ?? $"{Request.Scheme}://{Request.Host}/api/google-play/auth/callback";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return BadRequest(new { error = "Google OAuth not configured" });
        }

        // Exchange code for tokens
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = request.Code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to exchange OAuth code: {Response}", responseContent);
            return BadRequest(new { error = "Failed to exchange authorization code" });
        }

        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var accessToken = tokenResponse.GetProperty("access_token").GetString();
        var refreshToken = tokenResponse.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

        // Store connection (tokens should be encrypted in production)
        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.GooglePlay);

        if (connection == null)
        {
            connection = new StoreConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                StoreType = StoreType.GooglePlay,
                CreatedAt = DateTime.UtcNow
            };
            _context.StoreConnections.Add(connection);
        }

        // In production, encrypt these tokens!
        connection.GoogleAccessTokenEncrypted = System.Text.Encoding.UTF8.GetBytes(accessToken ?? "");
        if (refreshToken != null)
        {
            connection.GoogleRefreshTokenEncrypted = System.Text.Encoding.UTF8.GetBytes(refreshToken);
        }
        connection.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
        connection.IsActive = true;
        connection.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} connected Google Play account", userId);

        return Ok(new { success = true, connectionId = connection.Id });
    }

    [HttpGet("apps")]
    public async Task<IActionResult> GetApps()
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.GooglePlay && sc.IsActive);

        if (connection == null || connection.GoogleAccessTokenEncrypted == null)
        {
            return BadRequest(new { error = "Google Play not connected" });
        }

        // Return apps from database (synced separately)
        var apps = await _context.Apps
            .Where(a => a.StoreConnectionId == connection.Id)
            .Select(a => new
            {
                a.Id,
                a.AppName,
                a.PackageName,
                a.IconUrl,
                a.CreatedAt,
                SubscriptionCount = a.Subscriptions.Count
            })
            .ToListAsync();

        return Ok(apps);
    }

    [HttpPost("apps/{packageName}/sync")]
    public async Task<IActionResult> SyncApp(string packageName)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.GooglePlay && sc.IsActive);

        if (connection == null || connection.GoogleAccessTokenEncrypted == null)
        {
            return BadRequest(new { error = "Google Play not connected" });
        }

        // In a real implementation, this would call the Google Play API
        // For now, we'll just create a placeholder app

        var existingApp = await _context.Apps
            .FirstOrDefaultAsync(a => a.PackageName == packageName && a.UserId == userId);

        if (existingApp == null)
        {
            existingApp = new App
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                StoreConnectionId = connection.Id,
                StoreType = StoreType.GooglePlay,
                PackageName = packageName,
                AppName = packageName.Split('.').Last(),
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
            existingApp.PackageName,
            message = "App synced successfully"
        });
    }
}

public class OAuthCallbackRequest
{
    public string Code { get; set; } = string.Empty;
    public string? State { get; set; }
}
