using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using FluentValidation;
using MediatR;

namespace EMS.Application.Departments;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record UpdateDepartmentCommand(int Id, SaveDepartmentRequest Data) : IRequest;

internal sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator(IDepartmentRepository departments)
    {
        RuleFor(x => x.Data.Name).NotEmpty().MaximumLength(100).OverridePropertyName("Name");
        RuleFor(x => x.Data.Code).NotEmpty().MaximumLength(16).OverridePropertyName("Code");
        RuleFor(x => x.Data.Description).MaximumLength(500).OverridePropertyName("Description");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
                !await departments.NameOrCodeTakenAsync(cmd.Data.Name, cmd.Data.Code, cmd.Id, ct))
            .WithMessage("A department with the same name or code already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name) && !string.IsNullOrWhiteSpace(x.Data.Code));
    }
}

internal sealed class UpdateDepartmentCommandHandler(
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateDepartmentCommand>
{
    public async Task Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await departments.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Department), request.Id);

        department.Name = request.Data.Name.Trim();
        department.Code = request.Data.Code.Trim().ToUpperInvariant();
        department.Description = request.Data.Description?.Trim();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
