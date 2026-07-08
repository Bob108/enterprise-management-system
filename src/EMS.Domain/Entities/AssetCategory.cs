using EMS.Domain.Common;
using EMS.Shared.Enums;

namespace EMS.Domain.Entities;

/// <summary>Asset classification carrying the depreciation policy (design §6.3).</summary>
public class AssetCategory : BaseEntity, IAuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Prefix for generated asset codes, e.g. "ITE" → ITE-0001.</summary>
    public string CodePrefix { get; set; } = string.Empty;

    public DepreciationMethod Method { get; set; } = DepreciationMethod.StraightLine;
    public int UsefulLifeMonths { get; set; }

    /// <summary>Residual value as a fraction of cost (0.10 = 10%).</summary>
    public decimal ResidualRate { get; set; }

    public string? Description { get; set; }

    public List<Asset> Assets { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}
