using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Interfaces;
using SmartHotel.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;

namespace SmartHotel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly HotelDbContext _context;
    private readonly IAuthService _authService;

    public AuthController(HotelDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid email or password");
        }

        var token = _authService.GenerateJwtToken(user);
        var refreshToken = _authService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Token = token,
            RefreshToken = refreshToken,
            User = new
            {
                user.Id,
                user.Email,
                user.Role
            }
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);
        
        if (user == null || user.RefreshTokenExpiry < DateTime.Now)
        {
            return Unauthorized("Invalid or expired refresh token");
        }

        var newToken = _authService.GenerateJwtToken(user);
        var newRefreshToken = _authService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Token = newToken,
            RefreshToken = newRefreshToken
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            await _context.SaveChangesAsync();
        }
        return Ok("Logged out successfully");
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest("User already exists");
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = _authService.HashPassword(request.Password),
            Role = request.Role ?? "Guest"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok("User registered successfully");
    }
    [HttpGet("sessions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetActiveSessions()
    {
        var activeUsers = await _context.Users
            .Where(u => u.RefreshToken != null && u.RefreshTokenExpiry > DateTime.UtcNow)
            .Select(u => new 
            {
                u.Id,
                u.Username,
                u.Email,
                u.Role,
                u.FullName,
                LastActive = u.RefreshTokenExpiry!.Value.AddDays(-7) // Approximate
            })
            .ToListAsync();
            
        return Ok(activeUsers);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.FullName = request.FullName;
        user.Email = request.Email;
        
        await _context.SaveChangesAsync();
        return Ok(new { user.Id, user.Email, user.FullName, user.Role, user.Username });
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (!_authService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest("Incorrect current password");
        }

        user.PasswordHash = _authService.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();
        
        return Ok("Password updated successfully");
    }
}

public record UpdateProfileRequest(string FullName, string Email);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string Role);
public record RefreshRequest(string RefreshToken);
