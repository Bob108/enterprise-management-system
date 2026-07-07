namespace EMS.Domain.Common;

/// <summary>
/// Audit columns populated automatically by the persistence layer's SaveChanges
/// interceptor — never set these by hand in application code.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? ModifiedAtUtc { get; set; }
    string? ModifiedBy { get; set; }
}
