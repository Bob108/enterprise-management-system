using EMS.Application.Common.Security;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Employees;
using EMS.Shared.Enums;
using MediatR;

namespace EMS.Application.Employees;

[RequiresPermission(Permissions.Employees.View)]
public sealed record GetEmployeesQuery(
    string? Search,
    int? DepartmentId,
    EmploymentStatus? Status,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<EmployeeListItemDto>>;

internal sealed class GetEmployeesQueryHandler(IEmployeeRepository employees)
    : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeListItemDto>>
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<EmployeeListItemDto>> Handle(
        GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var (items, totalCount) = await employees.SearchAsync(
            new EmployeeSearchCriteria(
                request.Search?.Trim(), request.DepartmentId, request.Status, page, pageSize),
            cancellationToken);

        var dtos = items
            .Select(e => new EmployeeListItemDto(
                e.Id, e.EmployeeNumber, e.FullName, e.Email,
                e.Department.Name, e.Designation.Title, e.Status, e.HireDate))
            .ToList();

        return new PagedResult<EmployeeListItemDto>(dtos, page, pageSize, totalCount);
    }
}
