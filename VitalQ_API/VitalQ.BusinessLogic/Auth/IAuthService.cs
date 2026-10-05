using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
    Task<bool> RevokeRefreshTokenAsync(Guid userId);

    /// <summary>
    /// Centralized single source of truth for generating JWT access tokens & refresh tokens.
    /// </summary>
    AuthResponse GenerateToken(User user);
}
