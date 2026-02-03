using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PppPricing.API.Data;
using PppPricing.API.Services;
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
    private readonly IMemoryCache _cache;
    private readonly ICredentialEncryptionService _encryptionService;

    public GooglePlayController(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<GooglePlayController> logger,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ICredentialEncryptionService encryptionService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _cache = cache;
        _encryptionService = encryptionService;
    }

    private async Task<Guid?> GetUserIdAsync()
    {
        var firebaseUid = HttpContext.Items["FirebaseUid"]?.ToString();
        if (string.IsNullOrEmpty(firebaseUid)) return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        return user?.Id;
    }

    [HttpGet("auth/url")]
    public async Task<IActionResult> GetAuthUrl()
    {
        _logger.LogInformation("GetAuthUrl called");

        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            _logger.LogWarning("GetAuthUrl: User not authenticated");
            return Unauthorized();
        }

        _logger.LogInformation("GetAuthUrl: User {UserId} requesting OAuth URL", userId);

        var clientId = _configuration["Google:ClientId"];
        var redirectUri = _configuration["Google:RedirectUri"] ?? $"{Request.Scheme}://{Request.Host}/api/google-play/auth/callback";

        _logger.LogDebug("Google OAuth config - ClientId present: {HasClientId}, RedirectUri: {RedirectUri}",
            !string.IsNullOrEmpty(clientId), redirectUri);

        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogError("Google OAuth not configured - ClientId is missing");
            return BadRequest(new { error = "Google OAuth not configured" });
        }

        var scope = "https://www.googleapis.com/auth/androidpublisher";
        var stateToken = Guid.NewGuid().ToString();
        // Include userId in state so GET callback can identify user (format: "userId:stateToken")
        var state = $"{userId}:{stateToken}";

        // Store state token in cache for CSRF validation (expires in 10 minutes)
        _cache.Set($"oauth_state_{stateToken}", userId.ToString(), TimeSpan.FromMinutes(10));

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
        _logger.LogInformation("OAuth callback received");

        var userId = await GetUserIdAsync();
        if (userId == null)
        {
            _logger.LogWarning("OAuth callback: User not authenticated");
            return Unauthorized();
        }

        _logger.LogInformation("OAuth callback for user {UserId}, code length: {CodeLength}",
            userId, request.Code?.Length ?? 0);

        // Validate OAuth state parameter (CSRF protection)
        var expectedState = _cache.Get<string>($"oauth_state_{userId}");
        if (string.IsNullOrEmpty(expectedState) || request.State != expectedState)
        {
            _logger.LogWarning("OAuth state mismatch for user {UserId}", userId);
            return BadRequest(new { error = "Invalid state parameter. Please try again." });
        }
        _cache.Remove($"oauth_state_{userId}");

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
            _logger.LogError("Failed to exchange OAuth code for user {UserId}. Status: {StatusCode}, Response: {Response}",
                userId, response.StatusCode, responseContent);
            return BadRequest(new { error = "Failed to exchange authorization code", details = responseContent });
        }

        _logger.LogInformation("Token exchange successful for user {UserId}", userId);

        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var accessToken = tokenResponse.GetProperty("access_token").GetString();
        var refreshToken = tokenResponse.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

        // Store connection with encrypted tokens
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

        // Encrypt tokens before storing
        connection.GoogleAccessTokenEncrypted = _encryptionService.Encrypt(accessToken ?? "");
        if (refreshToken != null)
        {
            connection.GoogleRefreshTokenEncrypted = _encryptionService.Encrypt(refreshToken);
        }
        connection.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
        connection.IsActive = true;
        connection.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} connected Google Play account", userId);

        return Ok(new { success = true, connectionId = connection.Id });
    }

    [HttpGet("auth/callback")]
    public async Task<IActionResult> HandleCallbackGet(
        [FromQuery] string code,
        [FromQuery] string state)
    {
        _logger.LogInformation("OAuth GET callback received with state: {State}", state);

        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3009";
        var redirectBase = $"{frontendUrl}/dashboard/connections";

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("OAuth GET callback: Missing code or state");
            return Redirect($"{redirectBase}?google=error&message=Missing+authorization+code");
        }

        // Parse state to extract userId and stateToken (format: "userId:stateToken")
        var stateParts = state.Split(':');
        if (stateParts.Length != 2 || !Guid.TryParse(stateParts[0], out var userId))
        {
            _logger.LogWarning("OAuth GET callback: Invalid state format");
            return Redirect($"{redirectBase}?google=error&message=Invalid+state+format");
        }
        var stateToken = stateParts[1];

        // Validate state token from cache (CSRF protection)
        var cachedUserId = _cache.Get<string>($"oauth_state_{stateToken}");
        if (string.IsNullOrEmpty(cachedUserId) || cachedUserId != userId.ToString())
        {
            _logger.LogWarning("OAuth GET callback: State mismatch for user {UserId}", userId);
            return Redirect($"{redirectBase}?google=error&message=Invalid+state.+Please+try+again.");
        }
        _cache.Remove($"oauth_state_{stateToken}");

        // Verify user exists
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            _logger.LogWarning("OAuth GET callback: User {UserId} not found", userId);
            return Redirect($"{redirectBase}?google=error&message=User+not+found");
        }

        var clientId = _configuration["Google:ClientId"];
        var clientSecret = _configuration["Google:ClientSecret"];
        var redirectUri = _configuration["Google:RedirectUri"] ?? $"{Request.Scheme}://{Request.Host}/api/google-play/auth/callback";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return Redirect($"{redirectBase}?google=error&message=Google+OAuth+not+configured");
        }

        // Exchange code for tokens
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to exchange OAuth code for user {UserId}. Status: {StatusCode}, Response: {Response}",
                userId, response.StatusCode, responseContent);
            return Redirect($"{redirectBase}?google=error&message=Failed+to+exchange+authorization+code");
        }

        _logger.LogInformation("Token exchange successful for user {UserId}", userId);

        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var accessToken = tokenResponse.GetProperty("access_token").GetString();
        var refreshToken = tokenResponse.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

        // Store connection with encrypted tokens
        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.GooglePlay);

        if (connection == null)
        {
            connection = new StoreConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StoreType = StoreType.GooglePlay,
                CreatedAt = DateTime.UtcNow
            };
            _context.StoreConnections.Add(connection);
        }

        // Encrypt tokens before storing
        connection.GoogleAccessTokenEncrypted = _encryptionService.Encrypt(accessToken ?? "");
        if (refreshToken != null)
        {
            connection.GoogleRefreshTokenEncrypted = _encryptionService.Encrypt(refreshToken);
        }
        connection.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
        connection.IsActive = true;
        connection.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} connected Google Play account via GET callback", userId);

        return Redirect($"{frontendUrl}/dashboard/connections/import-apps?store=googleplay");
    }

    [HttpGet("available-apps")]
    public async Task<IActionResult> GetAvailableApps()
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.GooglePlay && sc.IsActive);

        if (connection == null || connection.GoogleAccessTokenEncrypted == null)
        {
            return BadRequest(new { error = "Google Play not connected" });
        }

        // Note: The Google Play Publisher API (androidpublisher) doesn't have a direct endpoint
        // to list all apps in a developer account. Apps must be specified by package name.
        // This endpoint returns apps that have been previously imported, allowing re-sync.
        // For new apps, users need to provide the package name manually.

        var apps = await _context.Apps
            .Where(a => a.StoreConnectionId == connection.Id)
            .Select(a => new
            {
                packageName = a.PackageName,
                name = a.AppName,
                iconUrl = a.IconUrl,
                alreadyImported = true
            })
            .ToListAsync();

        return Ok(new
        {
            apps,
            note = "Google Play API requires package names to be specified manually. Enter your app package names below."
        });
    }

    [HttpPost("import-apps")]
    public async Task<IActionResult> ImportApps([FromBody] GooglePlayImportRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        if (request.PackageNames == null || request.PackageNames.Count == 0)
        {
            return BadRequest(new { error = "No package names provided for import" });
        }

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.GooglePlay && sc.IsActive);

        if (connection == null || connection.GoogleAccessTokenEncrypted == null)
        {
            return BadRequest(new { error = "Google Play not connected" });
        }

        var accessToken = await GetValidAccessToken(connection);
        if (accessToken == null)
        {
            return BadRequest(new { error = "Failed to get valid access token. Please reconnect Google Play." });
        }

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        var importedApps = new List<object>();
        var errors = new List<object>();

        foreach (var packageName in request.PackageNames)
        {
            // Check if app already exists
            var existingApp = await _context.Apps
                .FirstOrDefaultAsync(a => a.PackageName == packageName && a.UserId == userId);

            if (existingApp != null)
            {
                importedApps.Add(new { id = existingApp.Id, packageName, name = existingApp.AppName, alreadyExisted = true });
                continue;
            }

            try
            {
                // Verify the app exists and user has access by trying to create an edit
                var response = await _httpClient.PostAsync(
                    $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits",
                    new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    var editContent = await response.Content.ReadAsStringAsync();
                    var editJson = JsonSerializer.Deserialize<JsonElement>(editContent);
                    var editId = editJson.GetProperty("id").GetString();

                    // Get app details from the edit
                    var detailsResponse = await _httpClient.GetAsync(
                        $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}/details");

                    string appName = packageName.Split('.').LastOrDefault() ?? packageName;

                    if (detailsResponse.IsSuccessStatusCode)
                    {
                        var detailsContent = await detailsResponse.Content.ReadAsStringAsync();
                        var detailsJson = JsonSerializer.Deserialize<JsonElement>(detailsContent);
                        if (detailsJson.TryGetProperty("defaultLanguage", out var langProp))
                        {
                            var lang = langProp.GetString();
                            // Try to get listing for default language
                            var listingResponse = await _httpClient.GetAsync(
                                $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}/listings/{lang}");
                            if (listingResponse.IsSuccessStatusCode)
                            {
                                var listingContent = await listingResponse.Content.ReadAsStringAsync();
                                var listingJson = JsonSerializer.Deserialize<JsonElement>(listingContent);
                                if (listingJson.TryGetProperty("title", out var titleProp))
                                {
                                    appName = titleProp.GetString() ?? appName;
                                }
                            }
                        }
                    }

                    // Delete the edit (we don't need to commit it)
                    await _httpClient.DeleteAsync(
                        $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}");

                    var newApp = new App
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId.Value,
                        StoreConnectionId = connection.Id,
                        StoreType = StoreType.GooglePlay,
                        PackageName = packageName,
                        AppName = appName,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Apps.Add(newApp);
                    importedApps.Add(new { id = newApp.Id, packageName, name = newApp.AppName, alreadyExisted = false });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to verify access to app {PackageName}: {Error}", packageName, errorContent);
                    errors.Add(new { packageName, error = "No access to this app or invalid package name" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing app {PackageName}", packageName);
                errors.Add(new { packageName, error = "Import failed" });
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} imported {Count} apps from Google Play", userId, importedApps.Count);

        return Ok(new { imported = importedApps.Count, apps = importedApps, errors });
    }

    private async Task<string?> GetValidAccessToken(StoreConnection connection)
    {
        // Check if current token is still valid (with 5 minute buffer)
        if (connection.GoogleTokenExpiry > DateTime.UtcNow.AddMinutes(5) &&
            connection.GoogleAccessTokenEncrypted != null && connection.GoogleAccessTokenEncrypted.Length > 0)
        {
            return _encryptionService.Decrypt(connection.GoogleAccessTokenEncrypted);
        }

        // Token expired or about to expire, try to refresh
        if (connection.GoogleRefreshTokenEncrypted == null || connection.GoogleRefreshTokenEncrypted.Length == 0)
        {
            return null;
        }

        var clientId = _configuration["Google:ClientId"];
        var clientSecret = _configuration["Google:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return null;
        }

        var refreshToken = _encryptionService.Decrypt(connection.GoogleRefreshTokenEncrypted);

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token"
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to refresh Google token");
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var newAccessToken = tokenResponse.GetProperty("access_token").GetString();
        var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

        // Update stored token
        connection.GoogleAccessTokenEncrypted = _encryptionService.Encrypt(newAccessToken ?? "");
        connection.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
        connection.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return newAccessToken;
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

    [HttpPost("apps/{packageName}/sync-subscriptions")]
    public async Task<IActionResult> SyncSubscriptions(string packageName)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.GooglePlay && sc.IsActive);

        if (connection == null || connection.GoogleAccessTokenEncrypted == null)
        {
            return BadRequest(new { error = "Google Play not connected" });
        }

        var app = await _context.Apps
            .Include(a => a.Subscriptions)
            .ThenInclude(s => s.Prices)
            .FirstOrDefaultAsync(a => a.PackageName == packageName && a.UserId == userId);

        if (app == null)
        {
            return NotFound(new { error = "App not found. Please import it first." });
        }

        var accessToken = await GetValidAccessToken(connection);
        if (accessToken == null)
        {
            return BadRequest(new { error = "Failed to get valid access token. Please reconnect Google Play." });
        }

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        try
        {
            // Fetch subscriptions from Google Play API
            var subscriptionsUrl = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/subscriptions";
            var response = await _httpClient.GetAsync(subscriptionsUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch subscriptions for {PackageName}: {StatusCode} - {Error}",
                    packageName, response.StatusCode, errorContent);
                return BadRequest(new { error = "Failed to fetch subscriptions from Google Play", details = errorContent });
            }

            var content = await response.Content.ReadAsStringAsync();
            var subscriptionsJson = JsonSerializer.Deserialize<JsonElement>(content);

            var syncedSubscriptions = new List<object>();
            var syncedPrices = 0;

            if (subscriptionsJson.TryGetProperty("subscriptions", out var subscriptionsArray))
            {
                foreach (var subJson in subscriptionsArray.EnumerateArray())
                {
                    var productId = subJson.GetProperty("productId").GetString() ?? "";

                    // Process each base plan
                    if (subJson.TryGetProperty("basePlans", out var basePlansArray))
                    {
                        foreach (var basePlanJson in basePlansArray.EnumerateArray())
                        {
                            var basePlanId = basePlanJson.GetProperty("basePlanId").GetString() ?? "";

                            // Get billing period from auto-renewing or prepaid config
                            string? billingPeriod = null;
                            if (basePlanJson.TryGetProperty("autoRenewingBasePlanType", out var autoRenewing))
                            {
                                if (autoRenewing.TryGetProperty("billingPeriodDuration", out var duration))
                                {
                                    billingPeriod = ParseBillingPeriod(duration.GetString());
                                }
                            }
                            else if (basePlanJson.TryGetProperty("prepaidBasePlanType", out var prepaid))
                            {
                                if (prepaid.TryGetProperty("billingPeriodDuration", out var duration))
                                {
                                    billingPeriod = ParseBillingPeriod(duration.GetString());
                                }
                            }

                            // Find or create subscription
                            var subscription = app.Subscriptions
                                .FirstOrDefault(s => s.ProductId == productId && s.BasePlanId == basePlanId);

                            if (subscription == null)
                            {
                                subscription = new Subscription
                                {
                                    Id = Guid.NewGuid(),
                                    AppId = app.Id,
                                    ProductId = productId,
                                    BasePlanId = basePlanId,
                                    BillingPeriod = billingPeriod,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                _context.Subscriptions.Add(subscription);
                                app.Subscriptions.Add(subscription);
                            }
                            else
                            {
                                subscription.BillingPeriod = billingPeriod;
                                subscription.UpdatedAt = DateTime.UtcNow;
                            }

                            // Process regional prices
                            if (basePlanJson.TryGetProperty("regionalConfigs", out var regionalConfigs))
                            {
                                foreach (var regionConfig in regionalConfigs.EnumerateArray())
                                {
                                    var regionCode = regionConfig.GetProperty("regionCode").GetString() ?? "";

                                    decimal? price = null;
                                    string? currencyCode = null;

                                    if (regionConfig.TryGetProperty("price", out var priceJson))
                                    {
                                        currencyCode = priceJson.TryGetProperty("currencyCode", out var cc)
                                            ? cc.GetString()
                                            : null;

                                        // Parse price from units and nanos
                                        var units = priceJson.TryGetProperty("units", out var u)
                                            ? long.Parse(u.GetString() ?? "0")
                                            : 0;
                                        var nanos = priceJson.TryGetProperty("nanos", out var n)
                                            ? n.GetInt32()
                                            : 0;
                                        price = units + (nanos / 1_000_000_000m);
                                    }

                                    // Find or create price record
                                    var subscriptionPrice = subscription.Prices
                                        .FirstOrDefault(p => p.RegionCode == regionCode);

                                    if (subscriptionPrice == null)
                                    {
                                        subscriptionPrice = new SubscriptionPrice
                                        {
                                            Id = Guid.NewGuid(),
                                            SubscriptionId = subscription.Id,
                                            RegionCode = regionCode,
                                            CurrencyCode = currencyCode ?? "",
                                            CurrentPrice = price,
                                            LastSyncedAt = DateTime.UtcNow
                                        };
                                        _context.SubscriptionPrices.Add(subscriptionPrice);
                                        subscription.Prices.Add(subscriptionPrice);
                                    }
                                    else
                                    {
                                        subscriptionPrice.CurrentPrice = price;
                                        subscriptionPrice.CurrencyCode = currencyCode ?? subscriptionPrice.CurrencyCode;
                                        subscriptionPrice.LastSyncedAt = DateTime.UtcNow;
                                    }
                                    syncedPrices++;
                                }
                            }

                            syncedSubscriptions.Add(new
                            {
                                id = subscription.Id,
                                productId,
                                basePlanId,
                                billingPeriod,
                                priceCount = subscription.Prices.Count
                            });
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Synced {SubscriptionCount} subscriptions with {PriceCount} prices for {PackageName}",
                syncedSubscriptions.Count, syncedPrices, packageName);

            return Ok(new
            {
                success = true,
                subscriptionCount = syncedSubscriptions.Count,
                priceCount = syncedPrices,
                subscriptions = syncedSubscriptions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing subscriptions for {PackageName}", packageName);
            return BadRequest(new { error = "Failed to sync subscriptions", details = ex.Message });
        }
    }

    private static string? ParseBillingPeriod(string? duration)
    {
        if (string.IsNullOrEmpty(duration)) return null;

        // Google uses ISO 8601 duration format: P1M, P1Y, P1W, etc.
        return duration.ToUpperInvariant() switch
        {
            "P1W" => "weekly",
            "P1M" => "monthly",
            "P3M" => "quarterly",
            "P6M" => "semi-annual",
            "P1Y" => "yearly",
            _ => duration
        };
    }
}

public class OAuthCallbackRequest
{
    public string Code { get; set; } = string.Empty;
    public string? State { get; set; }
}

public class GooglePlayImportRequest
{
    public List<string> PackageNames { get; set; } = new();
}
