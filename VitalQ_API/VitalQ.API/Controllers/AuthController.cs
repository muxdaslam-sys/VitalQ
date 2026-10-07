using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    /// User login with plain-text password (POST /api/auth/login).
    /// Rate-limited to max 10 attempts per minute per IP to defend against brute-force attacks.
    /// </summary>
    [EnableRateLimiting("LoginLimiter")]
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
    /// Supports dual revocation: via JWT Claim (if token is still active) or via HttpOnly Cookie (if JWT has expired).
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
        {
            // Active JWT session: revoke by claims
            await _authService.RevokeRefreshTokenAsync(userId);
        }
        else
        {
            // Edge-case fallback: If the 60-min JWT expired prior to logout,
            // revoke by the HttpOnly refresh token cookie value directly.
            var refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authService.RevokeByRefreshTokenAsync(refreshToken);
            }
        }

        // Cleanly purge the HttpOnly cookie with matching path and security scope
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/"
        });

        return Ok(new { message = "Logged out successfully" });
    }

    // --- HELPER: Set Secure HttpOnly Cookie ---
    private void SetCookie(string refreshToken)
    {
        // 1. HttpOnly: Inaccessible to JavaScript (protects against XSS token exfiltration).
        // 2. SameSite: Lax in local HTTP development, None over HTTPS in production (permits cross-origin SPA requests).
        // 3. Secure: Automatically enforced when running over TLS/HTTPS.
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(7),
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });
    }
}
