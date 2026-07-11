using EMS.Domain.Common;
using EMS.Shared.Enums;

namespace EMS.Domain.Entities;

/// <summary>
/// Purchase order raised from an approved request (design §6.6). Receiving accumulates
/// per line and is guarded against over-receipt; partial deliveries are first-class.
/// </summary>
public class PurchaseOrder : BaseEntity, IAuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public int PurchaseRequestId { get; set; }
    public PurchaseRequest PurchaseRequest { get; set; } = null!;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateOnly? ExpectedDate { get; set; }
    public string? Notes { get; set; }
    public DateTime? IssuedAtUtc { get; set; }

    public List<PurchaseOrderLine> Lines { get; set; } = [];
    public List<GoodsReceivedNote> GoodsReceivedNotes { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public decimal TotalValue => Lines.Sum(l => l.OrderedQuantity * l.UnitPrice);

    public void Issue(DateTime nowUtc)
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new DomainException($"Order {OrderNumber} is {Status}; only draft orders can be issued.");
        }

        Status = PurchaseOrderStatus.Issued;
        IssuedAtUtc = nowUtc;
    }

    public void Cancel()
    {
        var anythingReceived = Lines.Any(l => l.ReceivedQuantity > 0);
        if (Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Issued) || anythingReceived)
        {
            throw new DomainException($"Order {OrderNumber} cannot be cancelled once goods have been received.");
        }

        Status = PurchaseOrderStatus.Cancelled;
    }

    /// <summary>
    /// Records a delivery: accumulates received quantities (rejecting over-receipt),
    /// creates the GRN record, and moves the order to Partially/FullyReceived.
    /// </summary>
    public GoodsReceivedNote Receive(
        string grnNumber,
        int? warehouseId,
        IReadOnlyList<(int LineId, int Quantity)> quantities,
        DateTime nowUtc,
        string? receivedBy,
        string? notes)
    {
        if (Status is not (PurchaseOrderStatus.Issued or PurchaseOrderStatus.PartiallyReceived))
        {
            throw new DomainException($"Order {OrderNumber} is {Status}; goods can only be received against an issued order.");
        }

        if (quantities.Count == 0 || quantities.All(q => q.Quantity <= 0))
        {
            throw new DomainException("Nothing to receive — all quantities are zero.");
        }

        var grn = new GoodsReceivedNote
        {
            PurchaseOrder = this,
            GrnNumber = grnNumber,
            WarehouseId = warehouseId,
            ReceivedAtUtc = nowUtc,
            ReceivedBy = receivedBy,
            Notes = notes,
        };

        foreach (var (lineId, quantity) in quantities.Where(q => q.Quantity > 0))
        {
            var line = Lines.SingleOrDefault(l => l.Id == lineId)
                ?? throw new DomainException($"Order {OrderNumber} has no line {lineId}.");

            var outstanding = line.OrderedQuantity - line.ReceivedQuantity;
            if (quantity > outstanding)
            {
                throw new DomainException(
                    $"Cannot receive {quantity} × '{line.Description}': only {outstanding} outstanding.");
            }

            line.ReceivedQuantity += quantity;
            grn.Lines.Add(new GrnLine { GoodsReceivedNote = grn, PurchaseOrderLineId = lineId, Quantity = quantity });
        }

        GoodsReceivedNotes.Add(grn);
        Status = Lines.All(l => l.ReceivedQuantity >= l.OrderedQuantity)
            ? PurchaseOrderStatus.FullyReceived
            : PurchaseOrderStatus.PartiallyReceived;

        return grn;
    }

    public void Close()
    {
        if (Status != PurchaseOrderStatus.FullyReceived)
        {
            throw new DomainException($"Order {OrderNumber} is {Status}; only fully received orders can be closed.");
        }

        Status = PurchaseOrderStatus.Closed;
    }
}

public class PurchaseOrderLine : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int? PurchaseRequestLineId { get; set; }
    public string Description { get; set; } = string.Empty;
    public ItemNature Nature { get; set; }
    public int? AssetCategoryId { get; set; }
    public int? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class GoodsReceivedNote : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public string GrnNumber { get; set; } = string.Empty;

    /// <summary>Destination for consumable lines; null when only assets were received.</summary>
    public int? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }

    public List<GrnLine> Lines { get; set; } = [];
}

public class GrnLine : BaseEntity
{
    public int GoodsReceivedNoteId { get; set; }
    public GoodsReceivedNote GoodsReceivedNote { get; set; } = null!;
    public int PurchaseOrderLineId { get; set; }
    public int Quantity { get; set; }
}
