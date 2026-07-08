using EMS.Domain.Common;
using EMS.Shared.Enums;

namespace EMS.Domain.Entities;

public class AssetAssignment : BaseEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly AssignedOn { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public string? ConditionOut { get; set; }
    public string? ConditionIn { get; set; }
}

public class AssetTransfer : BaseEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public int FromDepartmentId { get; set; }
    public int ToDepartmentId { get; set; }
    public DateOnly TransferredOn { get; set; }
    public string? Reason { get; set; }
}

public class AssetDisposal : BaseEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public DateOnly DisposedOn { get; set; }
    public DisposalMethod Method { get; set; }
    public decimal? Proceeds { get; set; }

    /// <summary>Proceeds minus book value at disposal; negative = loss.</summary>
    public decimal GainLoss { get; set; }

    public string? Reason { get; set; }
}

/// <summary>
/// Immutable monthly depreciation posting (design §6.3) — append-only, never edited;
/// corrections would post reversing entries.
/// </summary>
public class DepreciationEntry
{
    public long Id { get; set; }
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public decimal BookValueAfter { get; set; }
    public DateTime PostedAtUtc { get; set; }
}
