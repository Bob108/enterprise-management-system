namespace EMS.Domain.Common;

/// <summary>
/// Master data is never hard-deleted once referenced (design §7.2). A global EF query
/// filter hides soft-deleted rows; ledger tables do not implement this interface.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
