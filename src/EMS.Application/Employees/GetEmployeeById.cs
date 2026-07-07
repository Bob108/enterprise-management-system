using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using MediatR;

namespace EMS.Application.Employees;

[RequiresPermission(Permissions.Employees.View)]
public sealed record GetEmployeeByIdQuery(int Id) : IRequest<EmployeeDetailDto>;

internal sealed class GetEmployeeByIdQueryHandler(IEmployeeRepository employees)
    : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailDto>
{
    public async Task<EmployeeDetailDto> Handle(
        GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        var employee = await employees.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Employee), request.Id);

        return new EmployeeDetailDto(
            employee.Id,
            employee.EmployeeNumber,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.Phone,
            employee.DepartmentId,
            employee.Department.Name,
            employee.DesignationId,
            employee.Designation.Title,
            employee.Status,
            employee.HireDate,
            employee.DateOfBirth,
            employee.Address,
            employee.EmergencyContacts
                .Select(c => new EmergencyContactDto(c.Id, c.Name, c.Relationship, c.Phone))
                .ToList(),
            Convert.ToBase64String(employee.RowVersion));
    }
}
