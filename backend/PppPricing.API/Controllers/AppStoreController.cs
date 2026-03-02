using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.API.Services;
using PppPricing.Domain.Models;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/app-store")]
public class AppStoreController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AppStoreController> _logger;
    private readonly HttpClient _httpClient;
    private readonly ICredentialEncryptionService _encryptionService;

    public AppStoreController(
        ApplicationDbContext context,
        ILogger<AppStoreController> logger,
        IHttpClientFactory httpClientFactory,
        ICredentialEncryptionService encryptionService)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _encryptionService = encryptionService;
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
        // Encrypt private key before storing
        connection.ApplePrivateKeyEncrypted = _encryptionService.Encrypt(request.PrivateKey);
        connection.IsActive = true;
        connection.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} connected App Store account", userId);

        return Ok(new { success = true, connectionId = connection.Id });
    }

    [HttpGet("available-apps")]
    public async Task<IActionResult> GetAvailableApps()
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.AppStore && sc.IsActive);

        if (connection == null || connection.ApplePrivateKeyEncrypted == null)
        {
            return BadRequest(new { error = "App Store not connected" });
        }

        try
        {
            var privateKey = _encryptionService.Decrypt(connection.ApplePrivateKeyEncrypted);
            var token = GenerateJwtToken(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await _httpClient.GetAsync("https://api.appstoreconnect.apple.com/v1/apps?limit=200");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to fetch apps from App Store Connect: {Error}", errorContent);
                return BadRequest(new { error = "Failed to fetch apps from App Store Connect" });
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
            var apps = new List<object>();

            foreach (var app in jsonDoc.RootElement.GetProperty("data").EnumerateArray())
            {
                var attributes = app.GetProperty("attributes");
                apps.Add(new
                {
                    appStoreId = app.GetProperty("id").GetString(),
                    bundleId = attributes.GetProperty("bundleId").GetString(),
                    name = attributes.GetProperty("name").GetString(),
                    sku = attributes.TryGetProperty("sku", out var sku) ? sku.GetString() : null
                });
            }

            return Ok(apps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available apps from App Store Connect");
            return BadRequest(new { error = "Failed to fetch apps" });
        }
    }

    [HttpPost("import-apps")]
    public async Task<IActionResult> ImportApps([FromBody] ImportAppsRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        if (request.AppIds == null || request.AppIds.Count == 0)
        {
            return BadRequest(new { error = "No apps selected for import" });
        }

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.AppStore && sc.IsActive);

        if (connection == null || connection.ApplePrivateKeyEncrypted == null)
        {
            return BadRequest(new { error = "App Store not connected" });
        }

        try
        {
            var privateKey = _encryptionService.Decrypt(connection.ApplePrivateKeyEncrypted);
            var token = GenerateJwtToken(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var importedApps = new List<object>();

            foreach (var appId in request.AppIds)
            {
                // Check if app already exists
                var existingApp = await _context.Apps
                    .FirstOrDefaultAsync(a => a.AppStoreId == appId && a.UserId == userId);

                if (existingApp != null)
                {
                    importedApps.Add(new { id = existingApp.Id, name = existingApp.AppName, alreadyExisted = true });
                    continue;
                }

                // Fetch app details from App Store Connect
                var response = await _httpClient.GetAsync($"https://api.appstoreconnect.apple.com/v1/apps/{appId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch app {AppId} from App Store Connect", appId);
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                var appData = jsonDoc.RootElement.GetProperty("data");
                var attributes = appData.GetProperty("attributes");

                var newApp = new App
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    StoreConnectionId = connection.Id,
                    StoreType = StoreType.AppStore,
                    AppStoreId = appId,
                    BundleId = attributes.GetProperty("bundleId").GetString(),
                    AppName = attributes.GetProperty("name").GetString() ?? $"App {appId}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Apps.Add(newApp);
                importedApps.Add(new { id = newApp.Id, name = newApp.AppName, alreadyExisted = false });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} imported {Count} apps from App Store", userId, importedApps.Count);

            return Ok(new { imported = importedApps.Count, apps = importedApps });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing apps from App Store Connect");
            return BadRequest(new { error = "Failed to import apps" });
        }
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

    [HttpPost("apps/{appStoreId}/sync-subscriptions")]
    public async Task<IActionResult> SyncSubscriptions(string appStoreId)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.StoreType == StoreType.AppStore && sc.IsActive);

        if (connection == null || connection.ApplePrivateKeyEncrypted == null)
        {
            return BadRequest(new { error = "App Store not connected" });
        }

        var app = await _context.Apps
            .Include(a => a.Subscriptions)
            .ThenInclude(s => s.Prices)
            .FirstOrDefaultAsync(a => a.AppStoreId == appStoreId && a.UserId == userId);

        if (app == null)
        {
            return NotFound(new { error = "App not found. Please import it first." });
        }

        try
        {
            var privateKey = _encryptionService.Decrypt(connection.ApplePrivateKeyEncrypted);
            AppStoreMetadataSnapshot? metadataSnapshot = null;
            var metadataSynced = false;
            var appNameBefore = app.AppName;

            var appMetadataResponse = await SendAppleRequest(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey, $"https://api.appstoreconnect.apple.com/v1/apps/{appStoreId}");
            if (appMetadataResponse.IsSuccessStatusCode)
            {
                var appMetadataContent = await appMetadataResponse.Content.ReadAsStringAsync();
                var appMetadataJson = JsonDocument.Parse(appMetadataContent);
                var appData = appMetadataJson.RootElement.GetProperty("data");
                var attributes = appData.GetProperty("attributes");

                metadataSnapshot = new AppStoreMetadataSnapshot(
                    AppName: attributes.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null,
                    BundleId: attributes.TryGetProperty("bundleId", out var bundleProp) ? bundleProp.GetString() : null,
                    AppStoreId: appData.TryGetProperty("id", out var idProp) ? idProp.GetString() : appStoreId);
                metadataSynced = true;
            }
            else
            {
                _logger.LogWarning("Failed to fetch metadata for App Store app {AppStoreId}. Continuing with subscription sync.", appStoreId);
            }

            var syncedSubscriptions = new List<object>();
            var syncedPrices = 0;
            var deletedSubscriptionCount = 0;
            var deletedPriceCount = 0;
            var isFullSnapshot = true;
            var now = DateTime.UtcNow;

            // Step 1: Get subscription groups for the app
            var groupsUrl = $"https://api.appstoreconnect.apple.com/v1/apps/{appStoreId}/subscriptionGroups?limit=200";
            var groupsResponse = await SendAppleRequest(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey, groupsUrl);

            if (!groupsResponse.IsSuccessStatusCode)
            {
                var errorContent = await groupsResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch subscription groups for {AppStoreId}: {StatusCode} - {Error}",
                    appStoreId, groupsResponse.StatusCode, errorContent);
                return BadRequest(new { error = "Failed to fetch subscription groups from App Store Connect", details = errorContent });
            }

            var groupsContent = await groupsResponse.Content.ReadAsStringAsync();
            var groupsJson = JsonDocument.Parse(groupsContent);
            var subscriptionSnapshots = new List<AppStoreSubscriptionSnapshot>();

            foreach (var group in groupsJson.RootElement.GetProperty("data").EnumerateArray())
            {
                var groupId = group.GetProperty("id").GetString();
                if (string.IsNullOrEmpty(groupId))
                {
                    continue;
                }

                var subscriptionsUrl = $"https://api.appstoreconnect.apple.com/v1/subscriptionGroups/{groupId}/subscriptions?limit=200";
                var subscriptionsResponse = await SendAppleRequest(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey, subscriptionsUrl);

                if (!subscriptionsResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch subscriptions for group {GroupId}", groupId);
                    isFullSnapshot = false;
                    continue;
                }

                var subscriptionsContent = await subscriptionsResponse.Content.ReadAsStringAsync();
                var subscriptionsJson = JsonDocument.Parse(subscriptionsContent);

                foreach (var subData in subscriptionsJson.RootElement.GetProperty("data").EnumerateArray())
                {
                    var subscriptionId = subData.GetProperty("id").GetString() ?? "";
                    var attributes = subData.GetProperty("attributes");
                    var productId = attributes.GetProperty("productId").GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(productId))
                    {
                        continue;
                    }
                    var name = attributes.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    var subscriptionPeriod = attributes.TryGetProperty("subscriptionPeriod", out var periodProp)
                        ? ParseAppleBillingPeriod(periodProp.GetString())
                        : null;

                    var priceSnapshots = new List<AppStorePriceSnapshot>();

                    // Add small delay to avoid rate limiting (Apple ~300 req/min)
                    await Task.Delay(200);

                    var pricesUrl = $"https://api.appstoreconnect.apple.com/v1/subscriptions/{subscriptionId}/prices?include=subscriptionPricePoint,territory&limit=200";
                    var pricesResponse = await SendAppleRequest(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey, pricesUrl);

                    if (!pricesResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch prices for subscription {SubscriptionId}", subscriptionId);
                        isFullSnapshot = false;
                        subscriptionSnapshots.Add(new AppStoreSubscriptionSnapshot(productId, name, subscriptionPeriod, priceSnapshots));
                        continue;
                    }

                    var pricesContent = await pricesResponse.Content.ReadAsStringAsync();
                    var pricesJson = JsonDocument.Parse(pricesContent);

                    var pricePoints = new Dictionary<string, decimal>();
                    var territories = new Dictionary<string, string>();

                    if (pricesJson.RootElement.TryGetProperty("included", out var included))
                    {
                        foreach (var item in included.EnumerateArray())
                        {
                            var type = item.GetProperty("type").GetString();
                            var itemId = item.GetProperty("id").GetString() ?? "";

                            if (type == "subscriptionPricePoints" && item.TryGetProperty("attributes", out var ppAttrs))
                            {
                                if (ppAttrs.TryGetProperty("customerPrice", out var cp) &&
                                    decimal.TryParse(cp.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var customerPrice))
                                {
                                    pricePoints[itemId] = customerPrice;
                                }
                            }
                            else if (type == "territories" && item.TryGetProperty("attributes", out var terrAttrs))
                            {
                                var currency = terrAttrs.TryGetProperty("currency", out var curr)
                                    ? curr.GetString() ?? "USD"
                                    : "USD";
                                var normalizedTerritoryId = RegionCodeNormalizer.NormalizeToAlpha3(itemId) ?? itemId;
                                territories[normalizedTerritoryId] = currency;
                            }
                        }
                    }

                    foreach (var priceData in pricesJson.RootElement.GetProperty("data").EnumerateArray())
                    {
                        if (!priceData.TryGetProperty("relationships", out var relationships))
                        {
                            continue;
                        }

                        string? regionCode = null;
                        decimal? price = null;
                        string? currencyCode = null;

                        if (relationships.TryGetProperty("territory", out var territoryRel) &&
                            territoryRel.TryGetProperty("data", out var terrData))
                        {
                            var apiRegionCode = terrData.GetProperty("id").GetString();
                            regionCode = RegionCodeNormalizer.NormalizeToAlpha3(apiRegionCode) ?? apiRegionCode;
                            if (regionCode != null && territories.TryGetValue(regionCode, out var terrCurrency))
                            {
                                currencyCode = terrCurrency;
                            }
                        }

                        if (relationships.TryGetProperty("subscriptionPricePoint", out var pricePointRel) &&
                            pricePointRel.TryGetProperty("data", out var ppData))
                        {
                            var ppId = ppData.GetProperty("id").GetString();
                            if (ppId != null && pricePoints.TryGetValue(ppId, out var resolvedPrice))
                            {
                                price = resolvedPrice;
                            }
                        }

                        if (string.IsNullOrEmpty(regionCode))
                        {
                            continue;
                        }

                        priceSnapshots.Add(new AppStorePriceSnapshot(regionCode, currencyCode ?? "USD", price));
                    }

                    subscriptionSnapshots.Add(new AppStoreSubscriptionSnapshot(productId, name, subscriptionPeriod, priceSnapshots));
                }
            }

            await using var tx = await _context.Database.BeginTransactionAsync();

            if (metadataSnapshot != null)
            {
                if (!string.IsNullOrWhiteSpace(metadataSnapshot.AppName))
                {
                    app.AppName = metadataSnapshot.AppName;
                }
                app.BundleId = metadataSnapshot.BundleId ?? app.BundleId;
                app.AppStoreId = metadataSnapshot.AppStoreId ?? app.AppStoreId;
            }
            app.UpdatedAt = now;

            var existingSubscriptionsByProductId = app.Subscriptions
                .GroupBy(s => s.ProductId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var seenProductIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var snapshot in subscriptionSnapshots)
            {
                seenProductIds.Add(snapshot.ProductId);

                if (!existingSubscriptionsByProductId.TryGetValue(snapshot.ProductId, out var subscription))
                {
                    subscription = new Subscription
                    {
                        Id = Guid.NewGuid(),
                        AppId = app.Id,
                        ProductId = snapshot.ProductId,
                        Name = snapshot.Name,
                        BillingPeriod = snapshot.BillingPeriod,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _context.Subscriptions.Add(subscription);
                    app.Subscriptions.Add(subscription);
                    existingSubscriptionsByProductId[snapshot.ProductId] = subscription;
                }
                else
                {
                    subscription.Name = snapshot.Name ?? subscription.Name;
                    subscription.BillingPeriod = snapshot.BillingPeriod ?? subscription.BillingPeriod;
                    subscription.UpdatedAt = now;
                }

                var existingPricesByRegion = subscription.Prices
                    .GroupBy(p => p.RegionCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var seenRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var priceSnapshot in snapshot.Prices)
                {
                    seenRegions.Add(priceSnapshot.RegionCode);

                    if (!existingPricesByRegion.TryGetValue(priceSnapshot.RegionCode, out var subscriptionPrice))
                    {
                        subscriptionPrice = new SubscriptionPrice
                        {
                            Id = Guid.NewGuid(),
                            SubscriptionId = subscription.Id,
                            RegionCode = priceSnapshot.RegionCode,
                            CurrencyCode = priceSnapshot.CurrencyCode,
                            CurrentPrice = priceSnapshot.Price,
                            LastSyncedAt = now
                        };
                        _context.SubscriptionPrices.Add(subscriptionPrice);
                        subscription.Prices.Add(subscriptionPrice);
                        existingPricesByRegion[priceSnapshot.RegionCode] = subscriptionPrice;
                    }
                    else
                    {
                        subscriptionPrice.CurrentPrice = priceSnapshot.Price;
                        subscriptionPrice.CurrencyCode = priceSnapshot.CurrencyCode;
                        subscriptionPrice.LastSyncedAt = now;
                    }
                    syncedPrices++;
                }

                if (isFullSnapshot)
                {
                    var stalePrices = subscription.Prices
                        .Where(p => !seenRegions.Contains(p.RegionCode))
                        .ToList();
                    if (stalePrices.Count > 0)
                    {
                        deletedPriceCount += stalePrices.Count;
                        _context.SubscriptionPrices.RemoveRange(stalePrices);
                    }
                }

                syncedSubscriptions.Add(new
                {
                    id = subscription.Id,
                    productId = snapshot.ProductId,
                    name = snapshot.Name,
                    billingPeriod = snapshot.BillingPeriod,
                    priceCount = seenRegions.Count
                });
            }

            if (isFullSnapshot)
            {
                var staleSubscriptions = app.Subscriptions
                    .Where(s => !seenProductIds.Contains(s.ProductId))
                    .ToList();
                foreach (var staleSubscription in staleSubscriptions)
                {
                    deletedSubscriptionCount++;
                    deletedPriceCount += staleSubscription.Prices.Count;
                    _context.Subscriptions.Remove(staleSubscription);
                }
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("Synced {SubscriptionCount} subscriptions with {PriceCount} prices for App Store app {AppStoreId}",
                syncedSubscriptions.Count, syncedPrices, appStoreId);

            return Ok(new
            {
                success = true,
                subscriptionCount = syncedSubscriptions.Count,
                priceCount = syncedPrices,
                metadataSynced,
                deletedSubscriptionCount,
                deletedPriceCount,
                isFullSnapshot,
                appNameBefore,
                appNameAfter = app.AppName,
                subscriptions = syncedSubscriptions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing subscriptions for App Store app {AppStoreId}", appStoreId);
            return BadRequest(new { error = "Failed to sync subscriptions", details = ex.Message });
        }
    }

    private async Task<HttpResponseMessage> SendAppleRequest(string keyId, string issuerId, string privateKey, string url)
    {
        var token = GenerateJwtToken(keyId, issuerId, privateKey);

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return await _httpClient.GetAsync(url);
    }

    private sealed record AppStoreMetadataSnapshot(
        string? AppName,
        string? BundleId,
        string? AppStoreId);

    private sealed record AppStoreSubscriptionSnapshot(
        string ProductId,
        string? Name,
        string? BillingPeriod,
        List<AppStorePriceSnapshot> Prices);

    private sealed record AppStorePriceSnapshot(
        string RegionCode,
        string CurrencyCode,
        decimal? Price);

    private static string? ParseAppleBillingPeriod(string? period)
    {
        if (string.IsNullOrEmpty(period)) return null;

        return period.ToUpperInvariant() switch
        {
            "ONE_WEEK" => "weekly",
            "ONE_MONTH" => "monthly",
            "TWO_MONTHS" => "bi-monthly",
            "THREE_MONTHS" => "quarterly",
            "SIX_MONTHS" => "semi-annual",
            "ONE_YEAR" => "yearly",
            _ => period
        };
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

public class ImportAppsRequest
{
    public List<string> AppIds { get; set; } = new();
}
