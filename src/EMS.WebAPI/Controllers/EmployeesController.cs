using EMS.Application.Employees;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Employees;
using EMS.Shared.Enums;
using EMS.WebAPI.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EMS.WebAPI.Controllers;

[ApiController]
[Route("api/v1/employees")]
public sealed class EmployeesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Employees.View)]
    public async Task<ActionResult<PagedResult<EmployeeListItemDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? departmentId,
        [FromQuery] EmploymentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(
            new GetEmployeesQuery(search, departmentId, status, page, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Employees.View)]
    public async Task<ActionResult<EmployeeDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetEmployeeByIdQuery(id), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Employees.Create)]
    public async Task<ActionResult<int>> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateEmployeeCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Employees.Edit)]
    public async Task<IActionResult> Update(int id, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmployeeCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Employees.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEmployeeCommand(id), cancellationToken);
        return NoContent();
    }
}
