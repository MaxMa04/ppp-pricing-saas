using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(ApplicationDbContext context, ILogger<SubscriptionsController> logger)
    {
        _context = context;
        _logger = logger;
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
}
