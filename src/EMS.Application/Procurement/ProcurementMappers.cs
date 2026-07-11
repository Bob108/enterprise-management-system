using EMS.Domain.Entities;
using EMS.Shared.Procurement;

namespace EMS.Application.Procurement;

internal static class ProcurementMappers
{
    public static PurchaseRequestDetailDto ToDetailDto(this PurchaseRequest request) => new(
        request.Id,
        request.RequestNumber,
        request.RequestedByUserId,
        request.RequestedByName,
        request.DepartmentId,
        request.Department.Name,
        request.Justification,
        request.TotalEstimatedCost,
        request.Status,
        request.RequiresSecondApproval,
        request.FirstApprovedByName,
        request.FirstApprovedAtUtc,
        request.SecondApprovedByName,
        request.SecondApprovedAtUtc,
        request.RejectionReason,
        request.PurchaseOrderId,
        request.Lines
            .OrderBy(l => l.Id)
            .Select(l => new PurchaseRequestLineDto(
                l.Id, l.Description, l.Nature,
                l.AssetCategoryId, l.AssetCategory?.Name,
                l.InventoryItemId, l.InventoryItem?.Name,
                l.Quantity, l.EstimatedUnitCost))
            .ToList(),
        Convert.ToBase64String(request.RowVersion));

    public static PurchaseOrderDetailDto ToDetailDto(this PurchaseOrder order) => new(
        order.Id,
        order.OrderNumber,
        order.PurchaseRequestId,
        order.PurchaseRequest.RequestNumber,
        order.PurchaseRequest.Department.Name,
        order.SupplierId,
        order.Supplier.Name,
        order.Status,
        order.ExpectedDate,
        order.Notes,
        order.TotalValue,
        order.Lines
            .OrderBy(l => l.Id)
            .Select(l => new PurchaseOrderLineDto(
                l.Id, l.Description, l.Nature, l.AssetCategoryId,
                l.InventoryItemId, l.InventoryItem?.Name,
                l.OrderedQuantity, l.ReceivedQuantity, l.UnitPrice))
            .ToList(),
        order.GoodsReceivedNotes
            .OrderByDescending(g => g.Id)
            .Select(g => new GrnDto(
                g.Id, g.GrnNumber, g.Warehouse?.Name, g.ReceivedAtUtc, g.ReceivedBy, g.Notes,
                g.Lines
                    .Select(gl => new GrnLineDto(
                        gl.PurchaseOrderLineId,
                        order.Lines.FirstOrDefault(l => l.Id == gl.PurchaseOrderLineId)?.Description ?? string.Empty,
                        gl.Quantity))
                    .ToList()))
            .ToList(),
        Convert.ToBase64String(order.RowVersion));
}
