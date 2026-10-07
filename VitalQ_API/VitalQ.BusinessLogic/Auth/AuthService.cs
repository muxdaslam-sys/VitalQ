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

    // 1. LOGIN
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

    // 2. REFRESH TOKEN (Rotation)
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

    // 3. LOGOUT (Session Invalidation)
    public async Task<bool> RevokeRefreshTokenAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Revokes refresh token directly using the token string from the HttpOnly cookie.
    /// Uses the IX_Users_RefreshToken filtered B-tree index for O(1) instantaneous lookup.
    /// </summary>
    public async Task<bool> RevokeByRefreshTokenAsync(string refreshToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (user == null) return false;

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _context.SaveChangesAsync();
        return true;
    }

    // 4. CENTRAL JWT GENERATOR (Single source of truth across the entire system)
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

    // --- PRIVATE HELPERS ---
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
}
