using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.Domain.Models;

namespace PppPricing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ApplicationDbContext context, ILogger<AuthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyToken()
    {
        var firebaseUid = HttpContext.Items["FirebaseUid"]?.ToString();
        var email = HttpContext.Items["UserEmail"]?.ToString();
        var displayName = HttpContext.Items["UserName"]?.ToString();

        if (string.IsNullOrEmpty(firebaseUid))
        {
            return Unauthorized(new { error = "No valid Firebase token provided" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                FirebaseUid = firebaseUid,
                Email = email ?? "",
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new user with Firebase UID: {FirebaseUid}", firebaseUid);
        }
        else
        {
            // Update user info if changed
            var updated = false;
            if (email != null && user.Email != email)
            {
                user.Email = email;
                updated = true;
            }
            if (displayName != null && user.DisplayName != displayName)
            {
                user.DisplayName = displayName;
                updated = true;
            }
            if (updated)
            {
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            displayName = user.DisplayName,
            firebaseUid = user.FirebaseUid
        });
    }
}
