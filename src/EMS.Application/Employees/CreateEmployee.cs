using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Employees;
using FluentValidation;
using MediatR;

namespace EMS.Application.Employees;

[RequiresPermission(Permissions.Employees.Create)]
public sealed record CreateEmployeeCommand(CreateEmployeeRequest Data) : IRequest<int>;

internal sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator(
        IEmployeeRepository employees,
        IDepartmentRepository departments,
        IDesignationRepository designations,
        IDateTime clock)
    {
        RuleFor(x => x.Data.FirstName).NotEmpty().MaximumLength(100).OverridePropertyName("FirstName");
        RuleFor(x => x.Data.LastName).NotEmpty().MaximumLength(100).OverridePropertyName("LastName");
        RuleFor(x => x.Data.Phone).MaximumLength(32).OverridePropertyName("Phone");
        RuleFor(x => x.Data.Address).MaximumLength(500).OverridePropertyName("Address");
        RuleFor(x => x.Data.Status).IsInEnum().OverridePropertyName("Status");

        RuleFor(x => x.Data.Email)
            .NotEmpty().EmailAddress().MaximumLength(256)
            .OverridePropertyName("Email");
        RuleFor(x => x.Data.Email)
            .MustAsync(async (email, ct) => !await employees.EmailTakenAsync(email, null, ct))
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

        RuleFor(x => x.Data.HireDate)
            .NotEmpty()
            .OverridePropertyName("HireDate");
        RuleFor(x => x.Data.DateOfBirth)
            .LessThan(x => x.Data.HireDate)
            .WithMessage("Date of birth must be before the hire date.")
            .OverridePropertyName("DateOfBirth")
            .When(x => x.Data.DateOfBirth.HasValue);

        RuleForEach(x => x.Data.EmergencyContacts).ChildRules(contact =>
        {
            contact.RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            contact.RuleFor(c => c.Relationship).NotEmpty().MaximumLength(100);
            contact.RuleFor(c => c.Phone).NotEmpty().MaximumLength(32);
        }).OverridePropertyName("EmergencyContacts");

        _ = clock; // reserved for future date rules (e.g. max future hire date)
    }
}

internal sealed class CreateEmployeeCommandHandler(
    IEmployeeRepository employees,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateEmployeeCommand, int>
{
    public async Task<int> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var data = request.Data;
        var employee = new Employee
        {
            EmployeeNumber = await employees.GetNextEmployeeNumberAsync(cancellationToken),
            FirstName = data.FirstName.Trim(),
            LastName = data.LastName.Trim(),
            Email = data.Email.Trim(),
            Phone = data.Phone?.Trim(),
            DepartmentId = data.DepartmentId,
            DesignationId = data.DesignationId,
            Status = data.Status,
            HireDate = data.HireDate,
            DateOfBirth = data.DateOfBirth,
            Address = data.Address?.Trim(),
            EmergencyContacts = data.EmergencyContacts
                .Select(c => new EmergencyContact
                {
                    Name = c.Name.Trim(),
                    Relationship = c.Relationship.Trim(),
                    Phone = c.Phone.Trim(),
                })
                .ToList(),
        };

        employees.Add(employee);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return employee.Id;
    }
}
