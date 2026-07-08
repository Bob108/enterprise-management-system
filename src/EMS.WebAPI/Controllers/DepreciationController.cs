using EMS.Application.Assets;
using EMS.Shared.Authorization;
using EMS.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

/// <summary>
/// Manual depreciation posting for administrators. The daily catch-up job does the same
/// thing automatically; this endpoint exists for on-demand runs and demos.
/// </summary>
[ApiController]
[Route("api/v1/admin/depreciation")]
public sealed class DepreciationController(ISender mediator) : ControllerBase
{
    [HttpPost("{year:int}/{month:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<ActionResult<int>> Post(int year, int month, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new PostMonthlyDepreciationCommand(year, month), cancellationToken));
}
