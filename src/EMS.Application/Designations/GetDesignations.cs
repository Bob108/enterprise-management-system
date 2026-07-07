using EMS.Domain.Repositories;
using EMS.Shared.Employees;
using MediatR;

namespace EMS.Application.Designations;

/// <summary>No permission attribute: every authenticated user may read the list (dropdowns/filters).</summary>
public sealed record GetDesignationsQuery : IRequest<IReadOnlyList<DesignationDto>>;

internal sealed class GetDesignationsQueryHandler(IDesignationRepository designations)
    : IRequestHandler<GetDesignationsQuery, IReadOnlyList<DesignationDto>>
{
    public async Task<IReadOnlyList<DesignationDto>> Handle(
        GetDesignationsQuery request, CancellationToken cancellationToken)
    {
        var items = await designations.GetAllWithEmployeeCountAsync(cancellationToken);
        return items
            .Select(x => new DesignationDto(
                x.Designation.Id, x.Designation.Title, x.Designation.Description, x.EmployeeCount))
            .ToList();
    }
}
