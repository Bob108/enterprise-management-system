using System.ComponentModel.DataAnnotations;

namespace EMS.Domain.Common;

/// <summary>Base type for all persisted entities. Int identity keys per design §7.2.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    /// <summary>
    /// SQL Server rowversion; optimistic concurrency token on every mutable entity
    /// (design D-7). A stale update surfaces as a 409 to the client.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
