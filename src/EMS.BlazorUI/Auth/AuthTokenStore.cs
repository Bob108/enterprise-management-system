using EMS.Shared.Auth;

namespace EMS.BlazorUI.Auth;

/// <summary>
/// Holds the access token in memory only — never localStorage/sessionStorage (design
/// §9.1). A page reload drops it; <see cref="AuthClient.TryRefreshAsync"/> restores the
/// session from the HttpOnly refresh cookie during startup.
/// </summary>
public sealed class AuthTokenStore
{
    public AuthResponse? Session { get; private set; }

    public string? AccessToken => Session?.AccessToken;

    public bool HasValidToken => Session is not null && Session.ExpiresAtUtc > DateTime.UtcNow;

    public void Set(AuthResponse session) => Session = session;

    public void Clear() => Session = null;
}
