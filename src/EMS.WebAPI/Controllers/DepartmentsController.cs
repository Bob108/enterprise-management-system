using EMS.Application.Departments;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using EMS.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/departments")]
[Authorize]
public sealed class DepartmentsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetDepartmentsQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<ActionResult<int>> Create(SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateDepartmentCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Update(int id, SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateDepartmentCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Administration.Settings)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteDepartmentCommand(id), cancellationToken);
        return NoContent();
    }
}
