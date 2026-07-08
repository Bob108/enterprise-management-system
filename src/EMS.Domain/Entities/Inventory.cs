using EMS.Domain.Common;
using EMS.Shared.Enums;

namespace EMS.Domain.Entities;

public class Warehouse : BaseEntity, IAuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Location { get; set; }

    public List<StockLevel> StockLevels { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Bulk consumable (design §6.4) — serialized items are Assets, never inventory.</summary>
public class InventoryItem : BaseEntity, IAuditableEntity, ISoftDeletable
{
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }

    /// <summary>Unit of measure: pcs, box, litre, ream…</summary>
    public string Unit { get; set; } = "pcs";

    public string? Description { get; set; }

    public List<StockLevel> StockLevels { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Maintained per-warehouse balance. Mutated ONLY by the repository's atomic conditional
/// UPDATE (design §7.3) — never load-modify-save — so concurrent movements cannot
/// oversell. The transaction ledger is the source of truth; a nightly check reconciles.
/// </summary>
public class StockLevel : BaseEntity
{
    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public int Quantity { get; set; }
    public int MinimumQuantity { get; set; }
}

/// <summary>Append-only movement ledger (design §6.4). Rows are never edited or deleted.</summary>
public class InventoryTransaction
{
    public long Id { get; set; }
    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public InventoryTransactionType Type { get; set; }

    /// <summary>Signed: positive adds stock, negative removes it.</summary>
    public int QuantityChange { get; set; }

    public string? Reason { get; set; }

    /// <summary>Business reference (work order, GRN…) once those modules post here.</summary>
    public string? Reference { get; set; }

    public DateTime PerformedAtUtc { get; set; }
    public string? PerformedBy { get; set; }
}
