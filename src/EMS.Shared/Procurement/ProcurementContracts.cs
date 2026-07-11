using EMS.Shared.Enums;

namespace EMS.Shared.Procurement;

// ---------- Purchase requests ----------

public sealed record PurchaseRequestLineDto(
    int? Id,
    string Description,
    ItemNature Nature,
    int? AssetCategoryId,
    string? AssetCategoryName,
    int? InventoryItemId,
    string? InventoryItemName,
    int Quantity,
    decimal EstimatedUnitCost);

public sealed record PurchaseRequestListItemDto(
    int Id,
    string RequestNumber,
    string RequestedByName,
    string DepartmentName,
    decimal TotalEstimatedCost,
    PurchaseRequestStatus Status,
    bool RequiresSecondApproval,
    bool AwaitingSecondApproval,
    DateTime CreatedAtUtc);

public sealed record PurchaseRequestDetailDto(
    int Id,
    string RequestNumber,
    int RequestedByUserId,
    string RequestedByName,
    int DepartmentId,
    string DepartmentName,
    string? Justification,
    decimal TotalEstimatedCost,
    PurchaseRequestStatus Status,
    bool RequiresSecondApproval,
    string? FirstApprovedByName,
    DateTime? FirstApprovedAtUtc,
    string? SecondApprovedByName,
    DateTime? SecondApprovedAtUtc,
    string? RejectionReason,
    int? PurchaseOrderId,
    IReadOnlyList<PurchaseRequestLineDto> Lines,
    string RowVersion);

public sealed record SavePurchaseRequestLine(
    string Description,
    ItemNature Nature,
    int? AssetCategoryId,
    int? InventoryItemId,
    int Quantity,
    decimal EstimatedUnitCost);

public sealed record SavePurchaseRequestRequest(
    int DepartmentId,
    string? Justification,
    List<SavePurchaseRequestLine> Lines);

public sealed record RejectPurchaseRequestRequest(string Reason);

// ---------- Purchase orders ----------

public sealed record PurchaseOrderLineDto(
    int Id,
    string Description,
    ItemNature Nature,
    int? AssetCategoryId,
    int? InventoryItemId,
    string? InventoryItemName,
    int OrderedQuantity,
    int ReceivedQuantity,
    decimal UnitPrice);

public sealed record PurchaseOrderListItemDto(
    int Id,
    string OrderNumber,
    string RequestNumber,
    string SupplierName,
    decimal TotalValue,
    PurchaseOrderStatus Status,
    DateTime CreatedAtUtc);

public sealed record GrnLineDto(int PurchaseOrderLineId, string Description, int Quantity);

public sealed record GrnDto(
    int Id,
    string GrnNumber,
    string? WarehouseName,
    DateTime ReceivedAtUtc,
    string? ReceivedBy,
    string? Notes,
    IReadOnlyList<GrnLineDto> Lines);

public sealed record PurchaseOrderDetailDto(
    int Id,
    string OrderNumber,
    int PurchaseRequestId,
    string RequestNumber,
    string DepartmentName,
    int SupplierId,
    string SupplierName,
    PurchaseOrderStatus Status,
    DateOnly? ExpectedDate,
    string? Notes,
    decimal TotalValue,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    IReadOnlyList<GrnDto> Grns,
    string RowVersion);

public sealed record CreatePurchaseOrderLine(int PurchaseRequestLineId, decimal UnitPrice);

public sealed record CreatePurchaseOrderRequest(
    int SupplierId,
    DateOnly? ExpectedDate,
    string? Notes,
    List<CreatePurchaseOrderLine> Lines);

public sealed record ReceiveGoodsLine(int PurchaseOrderLineId, int Quantity);

public sealed record ReceiveGoodsRequest(
    int? WarehouseId,
    string? Notes,
    List<ReceiveGoodsLine> Lines);
