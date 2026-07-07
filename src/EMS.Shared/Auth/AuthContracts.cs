namespace EMS.Shared.Auth;

public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Returned by login and refresh. The refresh token itself travels only in an
/// HttpOnly cookie (design §9.1) and never appears in a response body.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    int UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record UserDto(
    int Id,
    string Email,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles);
