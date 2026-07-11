using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Enums;
using EMS.Shared.Procurement;
using FluentValidation;
using MediatR;

namespace EMS.Application.Procurement;

/// <summary>
/// The integration moment of the whole system (design §6.6): each received line either
/// creates Assets (serialized) or posts a GrnReceipt into the inventory ledger
/// (consumable). GRN, PO updates, new assets and stock all commit in ONE transaction.
/// </summary>
[RequiresPermission(Permissions.Procurement.ReceiveGoods)]
public sealed record ReceiveGoodsCommand(int PurchaseOrderId, ReceiveGoodsRequest Data) : IRequest<int>;

internal sealed class ReceiveGoodsCommandValidator : AbstractValidator<ReceiveGoodsCommand>
{
    public ReceiveGoodsCommandValidator(IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Data.Notes).MaximumLength(500).OverridePropertyName("Notes");
        RuleFor(x => x.Data.Lines)
            .NotEmpty().WithMessage("Enter at least one received quantity.")
            .Must(l => l.Select(x => x.PurchaseOrderLineId).Distinct().Count() == l.Count)
            .WithMessage("Duplicate order line.")
            .OverridePropertyName("Lines");
        RuleForEach(x => x.Data.Lines).ChildRules(line =>
            line.RuleFor(l => l.Quantity).GreaterThan(0)).OverridePropertyName("Lines");
        RuleFor(x => x.Data.WarehouseId!.Value)
            .MustAsync(warehouses.ExistsAsync)
            .WithMessage("Selected warehouse does not exist.")
            .OverridePropertyName("WarehouseId")
            .When(x => x.Data.WarehouseId.HasValue);
    }
}

internal sealed class ReceiveGoodsCommandHandler(
    IProcurementRepository procurement,
    IAssetRepository assets,
    IAssetCategoryRepository categories,
    IInventoryRepository inventory,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTime clock) : IRequestHandler<ReceiveGoodsCommand, int>
{
    public async Task<int> Handle(ReceiveGoodsCommand request, CancellationToken cancellationToken)
    {
        var order = await procurement.GetOrderByIdAsync(request.PurchaseOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.PurchaseOrderId);

        var receivingConsumables = request.Data.Lines.Any(l =>
            order.Lines.FirstOrDefault(ol => ol.Id == l.PurchaseOrderLineId)?.Nature == ItemNature.Consumable);
        if (receivingConsumables && request.Data.WarehouseId is null)
        {
            throw new Common.Exceptions.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "WarehouseId", "Select a warehouse — this delivery includes consumables."),
            ]);
        }

        var now = clock.UtcNow;
        var grnNumber = await procurement.GetNextGrnNumberAsync(cancellationToken);

        // The PO state machine validates outstanding quantities and records the GRN.
        var grn = order.Receive(
            grnNumber,
            request.Data.WarehouseId,
            request.Data.Lines.Select(l => (l.PurchaseOrderLineId, l.Quantity)).ToList(),
            now,
            currentUser.UserName,
            request.Data.Notes?.Trim());

        // Asset codes are generated per category in local sequence — one DB round trip
        // per prefix, then increments, so multiple units in one GRN never collide.
        var nextCodeNumbers = new Dictionary<string, int>();

        foreach (var grnLine in grn.Lines)
        {
            var orderLine = order.Lines.Single(l => l.Id == grnLine.PurchaseOrderLineId);

            if (orderLine.Nature == ItemNature.Consumable)
            {
                await inventory.StageReceiptAsync(new InventoryTransaction
                {
                    ItemId = orderLine.InventoryItemId!.Value,
                    WarehouseId = request.Data.WarehouseId!.Value,
                    Type = InventoryTransactionType.GrnReceipt,
                    QuantityChange = grnLine.Quantity,
                    Reason = $"Goods received against {order.OrderNumber}",
                    Reference = grnNumber,
                    PerformedAtUtc = now,
                    PerformedBy = currentUser.UserName,
                }, cancellationToken);
            }
            else
            {
                var category = await categories.GetByIdAsync(orderLine.AssetCategoryId!.Value, cancellationToken)
                    ?? throw new NotFoundException(nameof(AssetCategory), orderLine.AssetCategoryId.Value);

                if (!nextCodeNumbers.TryGetValue(category.CodePrefix, out var next))
                {
                    var lastCode = await assets.GetNextAssetCodeAsync(category.CodePrefix, cancellationToken);
                    next = int.Parse(lastCode[(category.CodePrefix.Length + 1)..]);
                }

                for (var i = 0; i < grnLine.Quantity; i++)
                {
                    assets.Add(new Asset
                    {
                        AssetCode = $"{category.CodePrefix}-{next++:D4}",
                        Name = orderLine.Description,
                        CategoryId = category.Id,
                        DepartmentId = order.PurchaseRequest.DepartmentId,
                        SupplierId = order.SupplierId,
                        PurchaseDate = DateOnly.FromDateTime(now),
                        PurchaseCost = orderLine.UnitPrice,
                        Notes = $"Received via {grnNumber} against {order.OrderNumber}",
                    });
                }

                nextCodeNumbers[category.CodePrefix] = next;
            }
        }

        // One atomic commit: GRN + line accumulations + PO status + assets + stock + ledger.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return grn.Id;
    }
}
