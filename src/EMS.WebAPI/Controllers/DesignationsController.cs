using EMS.Application.Designations;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using EMS.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/designations")]
[Authorize]
public sealed class DesignationsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DesignationDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetDesignationsQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<ActionResult<int>> Create(SaveDesignationRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateDesignationCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Update(int id, SaveDesignationRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateDesignationCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteDesignationCommand(id), cancellationToken);
        return NoContent();
    }
}
