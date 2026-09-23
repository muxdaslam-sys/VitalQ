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

    public AuthService(VitalQDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // 1. LOGIN (Plain-text password check)
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Username == request.Username &&
            u.Password == request.Password &&
            u.IsActive);

        if (user == null) return null;

        var accessToken = CreateJwtToken(user);
        var refreshToken = Guid.NewGuid().ToString();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:DurationInMinutes"] ?? "15")),
            User = MapUser(user)
        };
    }

    // 2. REGISTER PATIENT
    public async Task<AuthResponse> RegisterPatientAsync(PatientRegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            throw new InvalidOperationException($"Username '{request.Username}' is already taken.");

        if (await _context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
            throw new InvalidOperationException($"Phone number '{request.PhoneNumber}' is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Password = request.Password, // Plain text as requested
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = "Patient",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            MedicalRecordNumber = $"MRN-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            CreatedAtUtc = DateTime.UtcNow
        };

        var accessToken = CreateJwtToken(user);
        var refreshToken = Guid.NewGuid().ToString();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        _context.Users.Add(user);
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:DurationInMinutes"] ?? "15")),
            User = MapUser(user)
        };
    }

    // 3. REFRESH TOKEN (Token Rotation)
    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == refreshToken &&
            u.RefreshTokenExpiryTime > DateTime.UtcNow &&
            u.IsActive);

        if (user == null) return null;

        var newAccessToken = CreateJwtToken(user);
        var newRefreshToken = Guid.NewGuid().ToString();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:DurationInMinutes"] ?? "15")),
            User = MapUser(user)
        };
    }

    // 4. REVOKE TOKEN (Logout)
    public async Task<bool> RevokeRefreshTokenAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _context.SaveChangesAsync();
        return true;
    }

    // --- PRIVATE HELPERS ---
    private string CreateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:DurationInMinutes"] ?? "15")),
            signingCredentials: creds
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
