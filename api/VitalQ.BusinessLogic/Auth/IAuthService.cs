using VitalQ.Entities.DTOs;
using VitalQ.Entities.Models;

namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Defines authentication and session operations: login, token rotation, session revocation, and JWT generation.
/// </summary>
public interface IAuthService
{
    #region 1. Authentication & Session Initiation
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    #endregion

    #region 2. Token Lifecycle & Rotation
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
    #endregion

    #region 3. Session Revocation & Sign-out
    Task<bool> RevokeRefreshTokenAsync(Guid userId);

    /// <summary>
    /// Revokes active session directly via the HttpOnly refresh token value.
    /// Critical fallback for logouts where the access token is already expired and User claims are absent.
    /// </summary>
    Task<bool> RevokeByRefreshTokenAsync(string refreshToken);
    #endregion

    #region 4. Cryptographic Token Generation
    /// <summary>
    /// Centralized single source of truth for generating JWT access tokens & refresh tokens.
    /// </summary>
    AuthResponse GenerateToken(User user);
    #endregion
}
