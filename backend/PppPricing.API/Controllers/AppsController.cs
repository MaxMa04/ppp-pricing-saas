using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AppsController> _logger;

    public AppsController(ApplicationDbContext context, ILogger<AppsController> logger)
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

    [HttpGet]
    public async Task<IActionResult> GetApps()
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var apps = await _context.Apps
            .Where(a => a.UserId == userId)
            .Select(a => new
            {
                a.Id,
                a.AppName,
                a.PackageName,
                a.BundleId,
                a.AppStoreId,
                a.StoreType,
                a.IconUrl,
                a.CreatedAt,
                SubscriptionCount = a.Subscriptions.Count
            })
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(apps);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetApp(Guid id)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var app = await _context.Apps
            .Where(a => a.Id == id && a.UserId == userId)
            .Select(a => new
            {
                a.Id,
                a.AppName,
                a.PackageName,
                a.BundleId,
                a.AppStoreId,
                a.StoreType,
                a.IconUrl,
                a.CreatedAt,
                a.UpdatedAt,
                Subscriptions = a.Subscriptions.Select(s => new
                {
                    s.Id,
                    s.ProductId,
                    s.BasePlanId,
                    s.Name,
                    s.BillingPeriod,
                    PriceCount = s.Prices.Count
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (app == null) return NotFound();

        return Ok(app);
    }

    [HttpGet("{id}/subscriptions")]
    public async Task<IActionResult> GetAppSubscriptions(Guid id)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var app = await _context.Apps.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (app == null) return NotFound();

        var subscriptions = await _context.Subscriptions
            .Where(s => s.AppId == id)
            .Select(s => new
            {
                s.Id,
                s.ProductId,
                s.BasePlanId,
                s.Name,
                s.BillingPeriod,
                s.CreatedAt,
                PriceCount = s.Prices.Count
            })
            .ToListAsync();

        return Ok(subscriptions);
    }
}
