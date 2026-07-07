using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using MediatR;

namespace EMS.Application.Designations;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record DeleteDesignationCommand(int Id) : IRequest;

internal sealed class DeleteDesignationCommandHandler(
    IDesignationRepository designations,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteDesignationCommand>
{
    public async Task Handle(DeleteDesignationCommand request, CancellationToken cancellationToken)
    {
        var designation = await designations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Designation), request.Id);

        var employeeCount = await designations.CountEmployeesAsync(request.Id, cancellationToken);
        if (employeeCount > 0)
        {
            throw new ConflictException(
                $"Designation '{designation.Title}' is assigned to {employeeCount} employee(s) and cannot be deleted.");
        }

        designations.Remove(designation); // converted to a soft delete by the audit interceptor
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
