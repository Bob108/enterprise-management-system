using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using FluentValidation;
using MediatR;

namespace EMS.Application.Departments;

[RequiresPermission(Permissions.Administration.Settings)]
public sealed record CreateDepartmentCommand(SaveDepartmentRequest Data) : IRequest<int>;

internal sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator(IDepartmentRepository departments)
    {
        RuleFor(x => x.Data.Name).NotEmpty().MaximumLength(100).OverridePropertyName("Name");
        RuleFor(x => x.Data.Code).NotEmpty().MaximumLength(16).OverridePropertyName("Code");
        RuleFor(x => x.Data.Description).MaximumLength(500).OverridePropertyName("Description");

        RuleFor(x => x.Data)
            .MustAsync(async (data, ct) => !await departments.NameOrCodeTakenAsync(data.Name, data.Code, null, ct))
            .WithMessage("A department with the same name or code already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name) && !string.IsNullOrWhiteSpace(x.Data.Code));
    }
}

internal sealed class CreateDepartmentCommandHandler(
    IDepartmentRepository departments,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateDepartmentCommand, int>
{
    public async Task<int> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = new Department
        {
            Name = request.Data.Name.Trim(),
            Code = request.Data.Code.Trim().ToUpperInvariant(),
            Description = request.Data.Description?.Trim(),
        };

        departments.Add(department);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return department.Id;
    }
}
