using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.Domain.Models;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PppController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PppController> _logger;

    public PppController(ApplicationDbContext context, ILogger<PppController> logger)
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

    [HttpGet("multipliers")]
    public async Task<IActionResult> GetMultipliers()
    {
        var multipliers = await _context.PppMultipliers
            .OrderBy(m => m.CountryName)
            .Select(m => new
            {
                m.Id,
                m.RegionCode,
                m.CountryName,
                m.Multiplier,
                m.Source,
                m.UpdatedAt
            })
            .ToListAsync();

        return Ok(multipliers);
    }

    [HttpGet("multipliers/{regionCode}")]
    public async Task<IActionResult> GetMultiplier(string regionCode)
    {
        var multiplier = await _context.PppMultipliers
            .FirstOrDefaultAsync(m => m.RegionCode == regionCode.ToUpper());

        if (multiplier == null) return NotFound();

        return Ok(new
        {
            multiplier.Id,
            multiplier.RegionCode,
            multiplier.CountryName,
            multiplier.Multiplier,
            multiplier.Source,
            multiplier.UpdatedAt
        });
    }

    [HttpPut("multipliers/{regionCode}")]
    public async Task<IActionResult> UpdateMultiplier(string regionCode, [FromBody] UpdateMultiplierRequest request)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var multiplier = await _context.PppMultipliers
            .FirstOrDefaultAsync(m => m.RegionCode == regionCode.ToUpper());

        if (multiplier == null)
        {
            multiplier = new PppMultiplier
            {
                Id = Guid.NewGuid(),
                RegionCode = regionCode.ToUpper(),
                CountryName = request.CountryName,
                Multiplier = request.Multiplier,
                Source = request.Source ?? "custom",
                UpdatedAt = DateTime.UtcNow
            };
            _context.PppMultipliers.Add(multiplier);
        }
        else
        {
            multiplier.Multiplier = request.Multiplier;
            if (request.CountryName != null) multiplier.CountryName = request.CountryName;
            if (request.Source != null) multiplier.Source = request.Source;
            multiplier.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            multiplier.Id,
            multiplier.RegionCode,
            multiplier.CountryName,
            multiplier.Multiplier,
            multiplier.Source,
            multiplier.UpdatedAt
        });
    }

    [HttpPost("multipliers/import")]
    public async Task<IActionResult> ImportMultipliers([FromBody] List<ImportMultiplierRequest> requests)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var imported = 0;
        var updated = 0;

        foreach (var request in requests)
        {
            var existing = await _context.PppMultipliers
                .FirstOrDefaultAsync(m => m.RegionCode == request.RegionCode.ToUpper());

            if (existing == null)
            {
                _context.PppMultipliers.Add(new PppMultiplier
                {
                    Id = Guid.NewGuid(),
                    RegionCode = request.RegionCode.ToUpper(),
                    CountryName = request.CountryName,
                    Multiplier = request.Multiplier,
                    Source = request.Source ?? "import",
                    UpdatedAt = DateTime.UtcNow
                });
                imported++;
            }
            else
            {
                existing.Multiplier = request.Multiplier;
                if (request.CountryName != null) existing.CountryName = request.CountryName;
                if (request.Source != null) existing.Source = request.Source;
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { imported, updated, total = imported + updated });
    }
}

public class UpdateMultiplierRequest
{
    public decimal Multiplier { get; set; }
    public string? CountryName { get; set; }
    public string? Source { get; set; }
}

public class ImportMultiplierRequest
{
    public string RegionCode { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
    public string? CountryName { get; set; }
    public string? Source { get; set; }
}
