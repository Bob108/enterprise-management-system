using EMS.Application.Common.Interfaces;
using EMS.Shared.Auth;
using EMS.Shared.Authorization;
using EMS.WebAPI.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IIdentityService identityService) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Users.View)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(CancellationToken cancellationToken)
        => Ok(await identityService.GetUsersAsync(cancellationToken));
}
