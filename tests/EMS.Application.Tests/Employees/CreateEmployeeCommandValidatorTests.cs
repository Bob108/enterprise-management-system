using EMS.Application.Common.Interfaces;
using EMS.Application.Employees;
using EMS.Domain.Repositories;
using EMS.Shared.Employees;
using EMS.Shared.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;
using NSubstitute;

namespace EMS.Application.Tests.Employees;

public class CreateEmployeeCommandValidatorTests
{
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IDepartmentRepository _departments = Substitute.For<IDepartmentRepository>();
    private readonly IDesignationRepository _designations = Substitute.For<IDesignationRepository>();
    private readonly IDateTime _clock = Substitute.For<IDateTime>();
    private readonly CreateEmployeeCommandValidator _validator;

    public CreateEmployeeCommandValidatorTests()
    {
        _employees.EmailTakenAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _departments.ExistsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _designations.ExistsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _clock.UtcNow.Returns(new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc));

        _validator = new CreateEmployeeCommandValidator(_employees, _departments, _designations, _clock);
    }

    private static CreateEmployeeRequest ValidRequest() => new(
        "Jane", "Doe", "jane.doe@northwind.example", "+254 700 000 001",
        DepartmentId: 1, DesignationId: 1, EmploymentStatus.Active,
        HireDate: new DateOnly(2024, 5, 1), DateOfBirth: new DateOnly(1990, 2, 14),
        Address: null, EmergencyContacts: []);

    [Fact]
    public async Task Valid_command_passes()
    {
        var result = await _validator.TestValidateAsync(new CreateEmployeeCommand(ValidRequest()));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Empty_first_name_fails()
    {
        var request = ValidRequest() with { FirstName = "" };

        var result = await _validator.TestValidateAsync(new CreateEmployeeCommand(request));

        result.ShouldHaveValidationErrorFor("FirstName");
    }

    [Fact]
    public async Task Invalid_email_fails()
    {
        var request = ValidRequest() with { Email = "not-an-email" };

        var result = await _validator.TestValidateAsync(new CreateEmployeeCommand(request));

        result.ShouldHaveValidationErrorFor("Email");
    }

    [Fact]
    public async Task Duplicate_email_fails()
    {
        _employees.EmailTakenAsync("jane.doe@northwind.example", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _validator.TestValidateAsync(new CreateEmployeeCommand(ValidRequest()));

        result.ShouldHaveValidationErrorFor("Email");
    }

    [Fact]
    public async Task Unknown_department_fails()
    {
        _departments.ExistsAsync(1, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _validator.TestValidateAsync(new CreateEmployeeCommand(ValidRequest()));

        result.ShouldHaveValidationErrorFor("DepartmentId");
    }

    [Fact]
    public async Task Date_of_birth_after_hire_date_fails()
    {
        var request = ValidRequest() with { DateOfBirth = new DateOnly(2025, 1, 1) };

        var result = await _validator.TestValidateAsync(new CreateEmployeeCommand(request));

        result.ShouldHaveValidationErrorFor("DateOfBirth");
    }

    [Fact]
    public async Task Emergency_contact_without_phone_fails()
    {
        var request = ValidRequest() with
        {
            EmergencyContacts = [new EmergencyContactDto(null, "John Doe", "Brother", "")],
        };

        var result = await _validator.TestValidateAsync(new CreateEmployeeCommand(request));

        result.Errors.Should().NotBeEmpty();
    }
}
