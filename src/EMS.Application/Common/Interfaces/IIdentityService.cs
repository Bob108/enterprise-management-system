using EMS.Shared.Auth;

namespace EMS.Application.Common.Interfaces;

/// <summary>User administration operations, implemented over ASP.NET Core Identity.</summary>
public interface IIdentityService
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
}
