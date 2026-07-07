namespace EMS.Domain.Entities;

/// <summary>
/// Append-only audit record (FR-14). Rows are written exclusively by the persistence
/// layer's audit interceptor — application code never creates or edits these.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Created | Modified | Deleted (soft deletes are recorded as Deleted).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>JSON map of changed properties: { "Name": { "old": "A", "new": "B" } }.</summary>
    public string? Changes { get; set; }

    public DateTime TimestampUtc { get; set; }
}
