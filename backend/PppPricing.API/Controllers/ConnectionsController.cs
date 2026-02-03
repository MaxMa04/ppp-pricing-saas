using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConnectionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConnectionsController> _logger;

    public ConnectionsController(ApplicationDbContext context, ILogger<ConnectionsController> logger)
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
    public async Task<IActionResult> GetConnections()
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connections = await _context.StoreConnections
            .Where(sc => sc.UserId == userId)
            .Select(sc => new
            {
                sc.Id,
                sc.StoreType,
                sc.IsActive,
                sc.CreatedAt,
                sc.UpdatedAt,
                HasGoogleTokens = sc.GoogleAccessTokenEncrypted != null,
                HasAppleKey = sc.ApplePrivateKeyEncrypted != null,
                AppCount = sc.Apps.Count
            })
            .ToListAsync();

        return Ok(connections);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteConnection(Guid id)
    {
        var userId = await GetUserIdAsync();
        if (userId == null) return Unauthorized();

        var connection = await _context.StoreConnections
            .FirstOrDefaultAsync(sc => sc.Id == id && sc.UserId == userId);

        if (connection == null) return NotFound();

        _context.StoreConnections.Remove(connection);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted store connection {ConnectionId}", userId, id);

        return NoContent();
    }
}
