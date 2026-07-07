using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using MediatR;

namespace EMS.Application.Employees;

[RequiresPermission(Permissions.Employees.Delete)]
public sealed record DeleteEmployeeCommand(int Id) : IRequest;

internal sealed class DeleteEmployeeCommandHandler(
    IEmployeeRepository employees,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteEmployeeCommand>
{
    public async Task Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await employees.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Employee), request.Id);

        employees.Remove(employee); // converted to a soft delete by the audit interceptor
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
