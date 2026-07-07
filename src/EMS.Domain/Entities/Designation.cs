using EMS.Domain.Common;

namespace EMS.Domain.Entities;

public class Designation : BaseEntity, IAuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<Employee> Employees { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}
