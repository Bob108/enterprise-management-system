using System.Security.Claims;
using EMS.Shared.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace EMS.BlazorUI.Auth;

/// <summary>
/// Projects the current session into a ClaimsPrincipal so AuthorizeView / [Authorize]
/// work client-side. UI checks are UX only — the API enforces every permission
/// server-side (design §9.2).
/// </summary>
public sealed class EmsAuthStateProvider(AuthTokenStore store) : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> Anonymous =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!store.HasValidToken)
        {
            return Anonymous;
        }

        var session = store.Session!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Name, session.DisplayName),
            new(ClaimTypes.Email, session.Email),
        };
        claims.AddRange(session.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(session.Permissions.Select(p => new Claim(Permissions.ClaimType, p)));

        var identity = new ClaimsIdentity(claims, authenticationType: "ems");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
