using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using PppPricing.API.Data;
using PppPricing.API.Services;
using PppPricing.Domain.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SubscriptionsController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ICredentialEncryptionService _encryptionService;

    public SubscriptionsController(
        ApplicationDbContext context,
        ILogger<SubscriptionsController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICredentialEncryptionService encryptionService)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _encryptionService = encryptionService;
    }

    private async Task<Guid?> GetUserIdAsync()
    {
        var firebaseUid = HttpContext.Items["FirebaseUid"]?.ToString();
        if (string.IsNullOrEmpty(firebaseUid)) return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        return user?.Id;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSubscription(Guid id)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var subscription = await _context.Subscriptions
            .Include(s => s.App)
            .Where(s => s.Id == id && s.App.UserId == userId)
            .Select(s => new
            {
                s.Id,
                s.ProductId,
                s.BasePlanId,
                s.Name,
                s.BillingPeriod,
                s.CreatedAt,
                s.UpdatedAt,
                App = new
                {
                    s.App.Id,
                    s.App.AppName,
                    s.App.StoreType
                }
            })
            .FirstOrDefaultAsync();

        if (subscription == null) return NotFound();

        return Ok(subscription);
    }

    [HttpGet("{id}/prices")]
    public async Task<IActionResult> GetSubscriptionPrices(Guid id)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var subscription = await _context.Subscriptions
            .Include(s => s.App)
            .FirstOrDefaultAsync(s => s.Id == id && s.App.UserId == userId);

        if (subscription == null) return NotFound();

        var prices = await _context.SubscriptionPrices
            .Where(p => p.SubscriptionId == id)
            .Select(p => new
            {
                p.Id,
                p.RegionCode,
                p.CurrencyCode,
                p.CurrentPrice,
                p.PppSuggestedPrice,
                p.PppMultiplier,
                p.LastSyncedAt,
                p.LastUpdatedAt,
                Difference = p.CurrentPrice.HasValue && p.PppSuggestedPrice.HasValue
                    ? Math.Round((double)((p.PppSuggestedPrice.Value - p.CurrentPrice.Value) / p.CurrentPrice.Value * 100), 2)
                    : (double?)null
            })
            .OrderBy(p => p.RegionCode)
            .ToListAsync();

        return Ok(prices);
    }

    [HttpPost("{id}/prices/preview")]
    public async Task<IActionResult> PreviewPriceChanges(Guid id)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var subscription = await _context.Subscriptions
            .Include(s => s.App)
            .Include(s => s.Prices)
            .FirstOrDefaultAsync(s => s.Id == id && s.App.UserId == userId);

        if (subscription == null) return NotFound();

        var pppMultipliers = await _context.PppMultipliers.ToDictionaryAsync(p => p.RegionCode, p => p.Multiplier);

        var previews = subscription.Prices.Select(price =>
        {
            var multiplier = pppMultipliers.GetValueOrDefault(price.RegionCode, 1.0m);
            var suggestedPrice = price.CurrentPrice.HasValue
                ? Math.Round(price.CurrentPrice.Value * multiplier, 2)
                : (decimal?)null;

            return new
            {
                price.RegionCode,
                price.CurrencyCode,
                price.CurrentPrice,
                SuggestedPrice = suggestedPrice,
                Multiplier = multiplier,
                Change = price.CurrentPrice.HasValue && suggestedPrice.HasValue
                    ? Math.Round((double)((suggestedPrice.Value - price.CurrentPrice.Value) / price.CurrentPrice.Value * 100), 2)
                    : (double?)null
            };
        }).ToList();

        var increases = previews.Count(p => p.Change > 0);
        var decreases = previews.Count(p => p.Change < 0);
        var unchanged = previews.Count(p => p.Change == 0 || p.Change == null);

        return Ok(new
        {
            subscription = new { subscription.Id, subscription.Name, subscription.ProductId },
            summary = new { increases, decreases, unchanged, total = previews.Count },
            prices = previews
        });
    }

    [HttpGet("{id}/prices/history")]
    public async Task<IActionResult> GetPriceHistory(Guid id)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var subscription = await _context.Subscriptions
            .Include(s => s.App)
            .FirstOrDefaultAsync(s => s.Id == id && s.App.UserId == userId);

        if (subscription == null) return NotFound();

        var history = await _context.PriceChanges
            .Where(pc => pc.SubscriptionId == id)
            .OrderByDescending(pc => pc.CreatedAt)
            .Take(100)
            .Select(pc => new
            {
                pc.Id,
                pc.RegionCode,
                pc.OldPrice,
                pc.NewPrice,
                pc.CurrencyCode,
                pc.ChangeType,
                pc.Status,
                pc.ErrorMessage,
                pc.CreatedAt,
                pc.AppliedAt
            })
            .ToListAsync();

        return Ok(history);
    }

    [HttpPost("{id}/prices/apply")]
    public async Task<IActionResult> ApplyPriceChanges(Guid id)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var subscription = await _context.Subscriptions
            .Include(s => s.App)
            .ThenInclude(a => a.StoreConnection)
            .Include(s => s.Prices)
            .FirstOrDefaultAsync(s => s.Id == id && s.App.UserId == userId);

        if (subscription == null) return NotFound();

        var app = subscription.App;
        var connection = app.StoreConnection;

        // Get PPP multipliers for the app's preferred index type
        var pppMultipliers = await _context.PppMultipliers
            .Where(m => m.IndexType == app.PreferredIndexType)
            .ToDictionaryAsync(p => p.RegionCode, p => p.Multiplier);

        // Calculate suggested prices
        var priceChanges = new List<PriceChangeResult>();
        foreach (var price in subscription.Prices)
        {
            var multiplier = pppMultipliers.GetValueOrDefault(price.RegionCode, 1.0m);
            var suggestedPrice = price.CurrentPrice.HasValue
                ? Math.Round(price.CurrentPrice.Value * multiplier, 2)
                : (decimal?)null;

            if (suggestedPrice.HasValue && price.CurrentPrice.HasValue && suggestedPrice != price.CurrentPrice)
            {
                priceChanges.Add(new PriceChangeResult
                {
                    RegionCode = price.RegionCode,
                    CurrencyCode = price.CurrencyCode,
                    OldPrice = price.CurrentPrice.Value,
                    NewPrice = suggestedPrice.Value
                });
            }
        }

        if (!priceChanges.Any())
        {
            return Ok(new { success = true, message = "No price changes to apply", appliedCount = 0 });
        }

        try
        {
            if (app.StoreType == StoreType.GooglePlay)
            {
                await ApplyGooglePlayPrices(subscription, connection, priceChanges, userId.Value);
            }
            else if (app.StoreType == StoreType.AppStore)
            {
                await ApplyAppStorePrices(subscription, connection, priceChanges, userId.Value);
            }

            // Update subscription prices in database
            foreach (var change in priceChanges.Where(c => c.Status == PriceChangeStatus.Applied))
            {
                var price = subscription.Prices.FirstOrDefault(p => p.RegionCode == change.RegionCode);
                if (price != null)
                {
                    price.PppSuggestedPrice = change.NewPrice;
                    price.PppMultiplier = pppMultipliers.GetValueOrDefault(change.RegionCode, 1.0m);
                    price.LastUpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            var appliedCount = priceChanges.Count(c => c.Status == PriceChangeStatus.Applied);
            var failedCount = priceChanges.Count(c => c.Status == PriceChangeStatus.Failed);

            return Ok(new
            {
                success = appliedCount > 0,
                appliedCount,
                failedCount,
                changes = priceChanges.Select(c => new
                {
                    c.RegionCode,
                    c.OldPrice,
                    c.NewPrice,
                    status = c.Status.ToString(),
                    c.ErrorMessage
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying price changes for subscription {SubscriptionId}", id);
            return BadRequest(new { error = "Failed to apply price changes", details = ex.Message });
        }
    }

    private async Task ApplyGooglePlayPrices(Subscription subscription, StoreConnection connection, List<PriceChangeResult> priceChanges, Guid userId)
    {
        var packageName = subscription.App.PackageName;

        if (string.IsNullOrEmpty(packageName) || connection.GoogleAccessTokenEncrypted == null)
        {
            foreach (var change in priceChanges)
            {
                change.Status = PriceChangeStatus.Failed;
                change.ErrorMessage = "Google Play not connected or invalid app";
            }
            return;
        }

        var accessToken = await GetValidGoogleAccessToken(connection);
        if (accessToken == null)
        {
            foreach (var change in priceChanges)
            {
                change.Status = PriceChangeStatus.Failed;
                change.ErrorMessage = "Failed to get valid access token";
            }
            return;
        }

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        // Step 1: Create an edit
        var createEditResponse = await _httpClient.PostAsync(
            $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        if (!createEditResponse.IsSuccessStatusCode)
        {
            var error = await createEditResponse.Content.ReadAsStringAsync();
            foreach (var change in priceChanges)
            {
                change.Status = PriceChangeStatus.Failed;
                change.ErrorMessage = $"Failed to create edit: {error}";
            }
            return;
        }

        var editContent = await createEditResponse.Content.ReadAsStringAsync();
        var editJson = JsonSerializer.Deserialize<JsonElement>(editContent);
        var editId = editJson.GetProperty("id").GetString();

        try
        {
            // Step 2: Update base plan prices
            var regionalConfigs = priceChanges.Select(change => new
            {
                regionCode = change.RegionCode,
                price = new
                {
                    currencyCode = change.CurrencyCode,
                    units = ((long)Math.Floor(change.NewPrice)).ToString(),
                    nanos = (int)((change.NewPrice - Math.Floor(change.NewPrice)) * 1_000_000_000)
                }
            }).ToList();

            var updatePayload = JsonSerializer.Serialize(new { regionalConfigs });

            var patchContent = new StringContent(updatePayload, System.Text.Encoding.UTF8, "application/json");
            var patchMethod = new HttpMethod("PATCH");
            var patchRequest = new HttpRequestMessage(patchMethod,
                $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/subscriptions/{subscription.ProductId}/basePlans/{subscription.BasePlanId}")
            {
                Content = patchContent
            };
            patchRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

            var patchResponse = await _httpClient.SendAsync(patchRequest);

            if (!patchResponse.IsSuccessStatusCode)
            {
                var error = await patchResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to update prices: {Error}", error);

                // Mark all changes as failed
                foreach (var change in priceChanges)
                {
                    change.Status = PriceChangeStatus.Failed;
                    change.ErrorMessage = $"Failed to update prices: {error}";
                    LogPriceChange(subscription.Id, userId, change);
                }

                // Delete the edit
                await _httpClient.DeleteAsync($"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}");
                return;
            }

            // Step 3: Commit the edit
            var commitResponse = await _httpClient.PostAsync(
                $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}:commit",
                null);

            if (commitResponse.IsSuccessStatusCode)
            {
                foreach (var change in priceChanges)
                {
                    change.Status = PriceChangeStatus.Applied;
                    LogPriceChange(subscription.Id, userId, change);
                }
            }
            else
            {
                var error = await commitResponse.Content.ReadAsStringAsync();
                foreach (var change in priceChanges)
                {
                    change.Status = PriceChangeStatus.Failed;
                    change.ErrorMessage = $"Failed to commit changes: {error}";
                    LogPriceChange(subscription.Id, userId, change);
                }
            }
        }
        catch
        {
            // Clean up edit on error
            await _httpClient.DeleteAsync($"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}");
            throw;
        }
    }

    private async Task ApplyAppStorePrices(Subscription subscription, StoreConnection connection, List<PriceChangeResult> priceChanges, Guid userId)
    {
        if (connection.ApplePrivateKeyEncrypted == null || string.IsNullOrEmpty(connection.AppleKeyId))
        {
            foreach (var change in priceChanges)
            {
                change.Status = PriceChangeStatus.Failed;
                change.ErrorMessage = "App Store not connected";
            }
            return;
        }

        var privateKey = _encryptionService.Decrypt(connection.ApplePrivateKeyEncrypted);

        // Get subscription ID from App Store (we need the App Store's subscription ID, not our internal ID)
        // First we need to find the subscription in App Store Connect
        var token = GenerateAppleJwtToken(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Find the subscription in App Store Connect by product ID
        var appStoreId = subscription.App.AppStoreId;
        var groupsResponse = await _httpClient.GetAsync(
            $"https://api.appstoreconnect.apple.com/v1/apps/{appStoreId}/subscriptionGroups?limit=200");

        if (!groupsResponse.IsSuccessStatusCode)
        {
            foreach (var change in priceChanges)
            {
                change.Status = PriceChangeStatus.Failed;
                change.ErrorMessage = "Failed to fetch subscription groups";
            }
            return;
        }

        string? appStoreSubscriptionId = null;

        var groupsContent = await groupsResponse.Content.ReadAsStringAsync();
        var groupsJson = JsonDocument.Parse(groupsContent);

        foreach (var group in groupsJson.RootElement.GetProperty("data").EnumerateArray())
        {
            var groupId = group.GetProperty("id").GetString();

            token = GenerateAppleJwtToken(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var subsResponse = await _httpClient.GetAsync(
                $"https://api.appstoreconnect.apple.com/v1/subscriptionGroups/{groupId}/subscriptions?limit=200");

            if (!subsResponse.IsSuccessStatusCode) continue;

            var subsContent = await subsResponse.Content.ReadAsStringAsync();
            var subsJson = JsonDocument.Parse(subsContent);

            foreach (var sub in subsJson.RootElement.GetProperty("data").EnumerateArray())
            {
                var attrs = sub.GetProperty("attributes");
                var productId = attrs.GetProperty("productId").GetString();

                if (productId == subscription.ProductId)
                {
                    appStoreSubscriptionId = sub.GetProperty("id").GetString();
                    break;
                }
            }

            if (appStoreSubscriptionId != null) break;
        }

        if (appStoreSubscriptionId == null)
        {
            foreach (var change in priceChanges)
            {
                change.Status = PriceChangeStatus.Failed;
                change.ErrorMessage = "Subscription not found in App Store Connect";
            }
            return;
        }

        // For App Store, we need to find the closest price point for each territory
        // This is more complex as Apple uses fixed price tiers
        // For now, log that manual intervention is needed for price points

        foreach (var change in priceChanges)
        {
            await Task.Delay(200); // Rate limiting

            token = GenerateAppleJwtToken(connection.AppleKeyId!, connection.AppleIssuerId!, privateKey);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            try
            {
                // Get available price points for this territory
                var pricePointsResponse = await _httpClient.GetAsync(
                    $"https://api.appstoreconnect.apple.com/v1/subscriptions/{appStoreSubscriptionId}/pricePoints?filter[territory]={change.RegionCode}&limit=200");

                if (!pricePointsResponse.IsSuccessStatusCode)
                {
                    change.Status = PriceChangeStatus.Failed;
                    change.ErrorMessage = "Failed to get price points";
                    LogPriceChange(subscription.Id, userId, change);
                    continue;
                }

                var pricePointsContent = await pricePointsResponse.Content.ReadAsStringAsync();
                var pricePointsJson = JsonDocument.Parse(pricePointsContent);

                // Find the closest price point
                string? closestPricePointId = null;
                decimal closestDiff = decimal.MaxValue;

                foreach (var pp in pricePointsJson.RootElement.GetProperty("data").EnumerateArray())
                {
                    var attrs = pp.GetProperty("attributes");
                    if (attrs.TryGetProperty("customerPrice", out var cpProp))
                    {
                        var customerPrice = decimal.Parse(cpProp.GetString() ?? "0");
                        var diff = Math.Abs(customerPrice - change.NewPrice);
                        if (diff < closestDiff)
                        {
                            closestDiff = diff;
                            closestPricePointId = pp.GetProperty("id").GetString();
                        }
                    }
                }

                if (closestPricePointId == null)
                {
                    change.Status = PriceChangeStatus.Failed;
                    change.ErrorMessage = "No suitable price point found";
                    LogPriceChange(subscription.Id, userId, change);
                    continue;
                }

                // Create a new price with the closest price point
                var pricePayload = new
                {
                    data = new
                    {
                        type = "subscriptionPrices",
                        attributes = new
                        {
                            startDate = (string?)null,
                            preserveCurrentPrice = false
                        },
                        relationships = new
                        {
                            subscription = new { data = new { type = "subscriptions", id = appStoreSubscriptionId } },
                            subscriptionPricePoint = new { data = new { type = "subscriptionPricePoints", id = closestPricePointId } },
                            territory = new { data = new { type = "territories", id = change.RegionCode } }
                        }
                    }
                };

                var pricePayloadJson = JsonSerializer.Serialize(pricePayload);
                var createPriceResponse = await _httpClient.PostAsync(
                    "https://api.appstoreconnect.apple.com/v1/subscriptionPrices",
                    new StringContent(pricePayloadJson, System.Text.Encoding.UTF8, "application/json"));

                if (createPriceResponse.IsSuccessStatusCode)
                {
                    change.Status = PriceChangeStatus.Applied;
                }
                else
                {
                    var error = await createPriceResponse.Content.ReadAsStringAsync();
                    change.Status = PriceChangeStatus.Failed;
                    change.ErrorMessage = $"Failed to set price: {error}";
                }

                LogPriceChange(subscription.Id, userId, change);
            }
            catch (Exception ex)
            {
                change.Status = PriceChangeStatus.Failed;
                change.ErrorMessage = ex.Message;
                LogPriceChange(subscription.Id, userId, change);
            }
        }
    }

    private void LogPriceChange(Guid subscriptionId, Guid userId, PriceChangeResult change)
    {
        var priceChange = new PriceChange
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            UserId = userId,
            RegionCode = change.RegionCode,
            OldPrice = change.OldPrice,
            NewPrice = change.NewPrice,
            CurrencyCode = change.CurrencyCode,
            ChangeType = PriceChangeType.PppAdjustment,
            Status = change.Status,
            ErrorMessage = change.ErrorMessage,
            CreatedAt = DateTime.UtcNow,
            AppliedAt = change.Status == PriceChangeStatus.Applied ? DateTime.UtcNow : null
        };
        _context.PriceChanges.Add(priceChange);
    }

    private async Task<string?> GetValidGoogleAccessToken(StoreConnection connection)
    {
        if (connection.GoogleTokenExpiry > DateTime.UtcNow.AddMinutes(5) &&
            connection.GoogleAccessTokenEncrypted != null && connection.GoogleAccessTokenEncrypted.Length > 0)
        {
            return _encryptionService.Decrypt(connection.GoogleAccessTokenEncrypted);
        }

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
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var newAccessToken = tokenResponse.GetProperty("access_token").GetString();
        var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

        connection.GoogleAccessTokenEncrypted = _encryptionService.Encrypt(newAccessToken ?? "");
        connection.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
        connection.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return newAccessToken;
    }

    private string GenerateAppleJwtToken(string keyId, string issuerId, string privateKeyPem)
    {
        var now = DateTimeOffset.UtcNow;

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

    private class PriceChangeResult
    {
        public string RegionCode { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public PriceChangeStatus Status { get; set; } = PriceChangeStatus.Pending;
        public string? ErrorMessage { get; set; }
    }
}
