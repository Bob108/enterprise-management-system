using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using MediatR;

namespace EMS.Application.Departments;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record DeleteDepartmentCommand(int Id) : IRequest;

internal sealed class DeleteDepartmentCommandHandler(
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteDepartmentCommand>
{
    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await departments.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Department), request.Id);

        var employeeCount = await departments.CountEmployeesAsync(request.Id, cancellationToken);
        if (employeeCount > 0)
        {
            throw new ConflictException(
                $"Department '{department.Name}' still has {employeeCount} employee(s) and cannot be deleted.");
        }

        departments.Remove(department); // converted to a soft delete by the audit interceptor
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
