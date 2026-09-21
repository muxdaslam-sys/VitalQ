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
    /// Create a patient account (§16 REST API).
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] PatientRegisterRequest request)
    {
        var response = await _authService.RegisterPatientAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Authenticate user and return JWT + refresh token.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        if (response == null) return Unauthorized(new { message = "Invalid credentials" });
        return Ok(response);
    }

    /// <summary>
    /// Exchange refresh token for a new JWT.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] string refreshToken)
    {
        var response = await _authService.RefreshTokenAsync(refreshToken);
        if (response == null) return Unauthorized(new { message = "Invalid refresh token" });
        return Ok(response);
    }
}
