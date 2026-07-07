using EMS.Domain.Repositories;
using EMS.Shared.Employees;
using MediatR;

namespace EMS.Application.Departments;

/// <summary>No permission attribute: every authenticated user may read the list (dropdowns/filters).</summary>
public sealed record GetDepartmentsQuery : IRequest<IReadOnlyList<DepartmentDto>>;

internal sealed class GetDepartmentsQueryHandler(IDepartmentRepository departments)
    : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<IReadOnlyList<DepartmentDto>> Handle(
        GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var items = await departments.GetAllWithEmployeeCountAsync(cancellationToken);
        return items
            .Select(x => new DepartmentDto(
                x.Department.Id, x.Department.Name, x.Department.Code,
                x.Department.Description, x.EmployeeCount))
            .ToList();
    }
}
