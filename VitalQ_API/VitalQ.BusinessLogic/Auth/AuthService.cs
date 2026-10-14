using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.DataAccess;
using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Services;

/// <summary>
/// Implements authentication and session management: login verification, token rotation, session revocation, and JWT generation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly VitalQDbContext _context;
    private readonly IConfiguration _config;
    private readonly SymmetricSecurityKey _key;
    private readonly SigningCredentials _creds;
    private readonly int _durationInMinutes;
    private readonly string _issuer;
    private readonly string _audience;

    public AuthService(VitalQDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;

        // Pre-cache key and signing credentials once on startup for maximum throughput
        var keyString = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        _creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        _durationInMinutes = int.Parse(_config["Jwt:DurationInMinutes"] ?? "15");
        _issuer = _config["Jwt:Issuer"] ?? "VitalQ";
        _audience = _config["Jwt:Audience"] ?? "VitalQ";
    }

    #region 1. Authentication & Session Initiation

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Username == request.Username &&
            u.Password == request.Password &&
            u.IsActive);

        if (user == null) return null;

        var response = GenerateToken(user);
        await _context.SaveChangesAsync();

        return response;
    }

    #endregion

    #region 2. Token Lifecycle & Rotation

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == refreshToken &&
            u.RefreshTokenExpiryTime > DateTime.UtcNow &&
            u.IsActive);

        if (user == null) return null;

        var response = GenerateToken(user);
        await _context.SaveChangesAsync();

        return response;
    }

    #endregion

    #region 3. Session Revocation & Sign-out

    /// <summary>
    /// Revokes active session by User ID.
    /// Uses atomic EF Core 8 ExecuteUpdateAsync for zero-allocation SQL execution.
    /// </summary>
    public async Task<bool> RevokeRefreshTokenAsync(Guid userId)
    {
        var rowsAffected = await _context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.RefreshToken, (string?)null)
                .SetProperty(u => u.RefreshTokenExpiryTime, (DateTime?)null));

        return rowsAffected > 0;
    }

    /// <summary>
    /// Revokes refresh token directly using the token string from the HttpOnly cookie.
    /// Uses the IX_Users_RefreshToken filtered B-tree index and atomic ExecuteUpdateAsync.
    /// </summary>
    public async Task<bool> RevokeByRefreshTokenAsync(string refreshToken)
    {
        var rowsAffected = await _context.Users
            .Where(u => u.RefreshToken == refreshToken)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.RefreshToken, (string?)null)
                .SetProperty(u => u.RefreshTokenExpiryTime, (DateTime?)null));

        return rowsAffected > 0;
    }

    #endregion

    #region 4. Cryptographic Token Generation & Internal Helpers

    /// <summary>
    /// Centralized single source of truth for generating JWT access tokens & refresh tokens.
    /// </summary>
    public AuthResponse GenerateToken(User user)
    {
        var accessToken = CreateJwtToken(user);
        var refreshToken = Guid.NewGuid().ToString("N");

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_durationInMinutes),
            User = MapUser(user)
        };
    }

    private string CreateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_durationInMinutes),
            signingCredentials: _creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserResponse MapUser(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        PhoneNumber = user.PhoneNumber,
        Role = user.Role,
        IsActive = user.IsActive,
        CreatedAtUtc = user.CreatedAtUtc
    };

    #endregion
}
