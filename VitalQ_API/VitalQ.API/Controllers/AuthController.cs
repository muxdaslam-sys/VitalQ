using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.Entities.DTOs;

namespace VitalQ.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a patient account (POST /api/auth/register).
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] PatientRegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterPatientAsync(request);
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                SetCookie(result.RefreshToken);
            }
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// User login with plain-text password (POST /api/auth/login).
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(new { message = "Invalid username or password" });

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetCookie(result.RefreshToken);
        }

        return Ok(result);
    }

    /// <summary>
    /// Exchange refresh token for a new access token (POST /api/auth/refresh).
    /// Reads token from HttpOnly cookie (or optional body for testing).
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] string? tokenFromBody)
    {
        var refreshToken = Request.Cookies["refreshToken"] ?? tokenFromBody;
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { message = "No refresh token provided" });

        var result = await _authService.RefreshTokenAsync(refreshToken);
        if (result == null)
            return Unauthorized(new { message = "Invalid or expired refresh token" });

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetCookie(result.RefreshToken);
        }

        return Ok(result);
    }

    /// <summary>
    /// Logout and invalidate refresh token (POST /api/auth/logout).
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
        {
            await _authService.RevokeRefreshTokenAsync(userId);
        }

        Response.Cookies.Delete("refreshToken");
        return Ok(new { message = "Logged out successfully" });
    }

    // --- HELPER: Set HttpOnly Cookie ---
    private void SetCookie(string refreshToken)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(7),
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }
}
