namespace EMS.Application.Common.Interfaces;

/// <summary>
/// Ambient identity of the acting user, implemented by the host (WebAPI) from the
/// authenticated principal. Handlers and interceptors depend on this, never on HttpContext.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int? UserId { get; }
    string? UserName { get; }
}
