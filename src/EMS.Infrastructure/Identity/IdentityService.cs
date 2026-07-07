using EMS.Application.Common.Interfaces;
using EMS.Infrastructure.Persistence;
using EMS.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Identity;

public sealed class IdentityService(EmsDbContext dbContext) : IIdentityService
{
    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.IsActive,
                Roles = dbContext.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return users
            .Select(u => new UserDto(u.Id, u.Email ?? string.Empty, u.DisplayName, u.IsActive, u.Roles))
            .ToList();
    }
}
