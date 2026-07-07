using EMS.Shared.Auth;

namespace EMS.Application.Common.Interfaces;

/// <summary>
/// Outcome of a successful login or refresh: the client-facing response plus the raw
/// refresh token for the host to place in an HttpOnly cookie.
/// </summary>
public sealed record AuthResult(
    AuthResponse Response,
    string RefreshToken,
    DateTime RefreshTokenExpiresUtc);

public interface IAuthService
{
    /// <summary>Null when credentials are invalid, the account is inactive, or locked out.</summary>
    Task<AuthResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the refresh token (one-time use). Presenting a revoked or expired token
    /// revokes the whole token family (theft detection) and returns null.
    /// </summary>
    Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes the presented token and its family. Safe to call with an unknown token.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}
