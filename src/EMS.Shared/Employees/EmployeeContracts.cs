using EMS.Shared.Enums;

namespace EMS.Shared.Employees;

public sealed record DepartmentDto(
    int Id, string Name, string Code, string? Description, int EmployeeCount);

public sealed record SaveDepartmentRequest(string Name, string Code, string? Description);

public sealed record DesignationDto(int Id, string Title, string? Description, int EmployeeCount);

public sealed record SaveDesignationRequest(string Title, string? Description);

public sealed record EmergencyContactDto(int? Id, string Name, string Relationship, string Phone);

public sealed record EmployeeListItemDto(
    int Id,
    string EmployeeNumber,
    string FullName,
    string Email,
    string DepartmentName,
    string DesignationTitle,
    EmploymentStatus Status,
    DateOnly HireDate);

public sealed record EmployeeDetailDto(
    int Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int DepartmentId,
    string DepartmentName,
    int DesignationId,
    string DesignationTitle,
    EmploymentStatus Status,
    DateOnly HireDate,
    DateOnly? DateOfBirth,
    string? Address,
    IReadOnlyList<EmergencyContactDto> EmergencyContacts,
    string RowVersion);

public sealed record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int DepartmentId,
    int DesignationId,
    EmploymentStatus Status,
    DateOnly HireDate,
    DateOnly? DateOfBirth,
    string? Address,
    List<EmergencyContactDto> EmergencyContacts);

public sealed record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int DepartmentId,
    int DesignationId,
    EmploymentStatus Status,
    DateOnly HireDate,
    DateOnly? DateOfBirth,
    string? Address,
    List<EmergencyContactDto> EmergencyContacts,
    string RowVersion);
