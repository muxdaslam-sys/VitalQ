using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
    Task<bool> RevokeRefreshTokenAsync(Guid userId);

    /// <summary>
    /// Revokes active session directly via the HttpOnly refresh token value.
    /// Critical fallback for logouts where the access token is already expired and User claims are absent.
    /// </summary>
    Task<bool> RevokeByRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Centralized single source of truth for generating JWT access tokens & refresh tokens.
    /// </summary>
    AuthResponse GenerateToken(User user);
}
