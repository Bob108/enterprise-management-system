using EMS.Domain.Common;
using EMS.Shared.Enums;

namespace EMS.Domain.Entities;

public class Employee : BaseEntity, IAuditableEntity, ISoftDeletable
{
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int DesignationId { get; set; }
    public Designation Designation { get; set; } = null!;

    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;
    public DateOnly HireDate { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }

    public List<EmergencyContact> EmergencyContacts { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}";

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}
