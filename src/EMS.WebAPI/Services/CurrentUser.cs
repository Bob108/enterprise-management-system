using System.Security.Claims;
using EMS.Application.Common.Interfaces;
using EMS.Shared.Authorization;

namespace EMS.WebAPI.Services;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int? UserId =>
        int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => Principal?.Identity?.Name;

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(Permissions.ClaimType, permission) ?? false;
}
