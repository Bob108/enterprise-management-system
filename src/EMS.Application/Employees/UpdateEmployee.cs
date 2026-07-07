using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using FluentValidation;
using MediatR;

namespace EMS.Application.Employees;

[RequiresPermission(Permissions.Employees.Edit)]
public sealed record UpdateEmployeeCommand(int Id, UpdateEmployeeRequest Data) : IRequest;

internal sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator(
        IEmployeeRepository employees,
        IDepartmentRepository departments,
        IDesignationRepository designations)
    {
        RuleFor(x => x.Data.FirstName).NotEmpty().MaximumLength(100).OverridePropertyName("FirstName");
        RuleFor(x => x.Data.LastName).NotEmpty().MaximumLength(100).OverridePropertyName("LastName");
        RuleFor(x => x.Data.Phone).MaximumLength(32).OverridePropertyName("Phone");
        RuleFor(x => x.Data.Address).MaximumLength(500).OverridePropertyName("Address");
        RuleFor(x => x.Data.Status).IsInEnum().OverridePropertyName("Status");

        RuleFor(x => x.Data.Email)
            .NotEmpty().EmailAddress().MaximumLength(256)
            .OverridePropertyName("Email");
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await employees.EmailTakenAsync(cmd.Data.Email, cmd.Id, ct))
            .WithMessage("An employee with this email already exists.")
            .OverridePropertyName("Email")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Email));

        RuleFor(x => x.Data.DepartmentId)
            .MustAsync(departments.ExistsAsync)
            .WithMessage("Selected department does not exist.")
            .OverridePropertyName("DepartmentId");
        RuleFor(x => x.Data.DesignationId)
            .MustAsync(designations.ExistsAsync)
            .WithMessage("Selected designation does not exist.")
            .OverridePropertyName("DesignationId");

        RuleFor(x => x.Data.HireDate).NotEmpty().OverridePropertyName("HireDate");
        RuleFor(x => x.Data.DateOfBirth)
            .LessThan(x => x.Data.HireDate)
            .WithMessage("Date of birth must be before the hire date.")
            .OverridePropertyName("DateOfBirth")
            .When(x => x.Data.DateOfBirth.HasValue);

        RuleFor(x => x.Data.RowVersion)
            .NotEmpty()
            .Must(v => IsValidBase64(v))
            .WithMessage("Invalid concurrency token.")
            .OverridePropertyName("RowVersion");

        RuleForEach(x => x.Data.EmergencyContacts).ChildRules(contact =>
        {
            contact.RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            contact.RuleFor(c => c.Relationship).NotEmpty().MaximumLength(100);
            contact.RuleFor(c => c.Phone).NotEmpty().MaximumLength(32);
        }).OverridePropertyName("EmergencyContacts");
    }

    private static bool IsValidBase64(string value)
    {
        var buffer = new Span<byte>(new byte[value.Length]);
        return Convert.TryFromBase64String(value, buffer, out _);
    }
}

internal sealed class UpdateEmployeeCommandHandler(
    IEmployeeRepository employees,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateEmployeeCommand>
{
    public async Task Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await employees.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.Id);

        var data = request.Data;

        // Optimistic concurrency: the save fails with 409 if the row changed since the
        // client loaded it (design §7.3).
        employees.SetOriginalRowVersion(employee, Convert.FromBase64String(data.RowVersion));

        employee.FirstName = data.FirstName.Trim();
        employee.LastName = data.LastName.Trim();
        employee.Email = data.Email.Trim();
        employee.Phone = data.Phone?.Trim();
        employee.DepartmentId = data.DepartmentId;
        employee.DesignationId = data.DesignationId;
        employee.Status = data.Status;
        employee.HireDate = data.HireDate;
        employee.DateOfBirth = data.DateOfBirth;
        employee.Address = data.Address?.Trim();

        ReconcileContacts(employee, data.EmergencyContacts);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ReconcileContacts(Employee employee, List<EmergencyContactDto> incoming)
    {
        employee.EmergencyContacts.RemoveAll(existing =>
            incoming.All(dto => dto.Id != existing.Id));

        foreach (var dto in incoming)
        {
            var existing = dto.Id.HasValue
                ? employee.EmergencyContacts.FirstOrDefault(c => c.Id == dto.Id.Value)
                : null;

            if (existing is null)
            {
                employee.EmergencyContacts.Add(new EmergencyContact
                {
                    Name = dto.Name.Trim(),
                    Relationship = dto.Relationship.Trim(),
                    Phone = dto.Phone.Trim(),
                });
            }
            else
            {
                existing.Name = dto.Name.Trim();
                existing.Relationship = dto.Relationship.Trim();
                existing.Phone = dto.Phone.Trim();
            }
        }
    }
}
