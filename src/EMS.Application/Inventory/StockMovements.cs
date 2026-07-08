using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Security;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Enums;
using EMS.Shared.Inventory;
using FluentValidation;
using MediatR;

namespace EMS.Application.Inventory;

// All movements go through IInventoryRepository.TryApplyMovementAsync — an atomic
// conditional UPDATE plus ledger row in one DB transaction (design §7.3). A refused
// movement (insufficient stock) surfaces as 409, and concurrent overselling is impossible.

[RequiresPermission(Permissions.Inventory.StockIn)]
public sealed record StockInCommand(int ItemId, StockMovementRequest Data) : IRequest;

[RequiresPermission(Permissions.Inventory.StockOut)]
public sealed record StockOutCommand(int ItemId, StockMovementRequest Data) : IRequest;

[RequiresPermission(Permissions.Inventory.Adjust)]
public sealed record AdjustStockCommand(int ItemId, StockAdjustmentRequest Data) : IRequest;

internal sealed class StockMovementValidator : AbstractValidator<StockMovementRequest>
{
    public StockMovementValidator(IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.WarehouseId)
            .MustAsync(warehouses.ExistsAsync)
            .WithMessage("Warehouse does not exist.");
    }
}

internal sealed class StockInCommandValidator : AbstractValidator<StockInCommand>
{
    public StockInCommandValidator(IWarehouseRepository warehouses)
        => RuleFor(x => x.Data).SetValidator(new StockMovementValidator(warehouses)).OverridePropertyName("");
}

internal sealed class StockOutCommandValidator : AbstractValidator<StockOutCommand>
{
    public StockOutCommandValidator(IWarehouseRepository warehouses)
        => RuleFor(x => x.Data).SetValidator(new StockMovementValidator(warehouses)).OverridePropertyName("");
}

internal sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator(IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Data.QuantityChange)
            .NotEqual(0).WithMessage("Adjustment cannot be zero.")
            .OverridePropertyName("QuantityChange");
        RuleFor(x => x.Data.Reason)
            .NotEmpty().WithMessage("Adjustments require a reason.")
            .MaximumLength(500)
            .OverridePropertyName("Reason");
        RuleFor(x => x.Data.WarehouseId)
            .MustAsync(warehouses.ExistsAsync)
            .WithMessage("Warehouse does not exist.")
            .OverridePropertyName("WarehouseId");
    }
}

internal sealed class StockMovementHandlers(
    IInventoryRepository inventory,
    ICurrentUser currentUser,
    IDateTime clock) :
    IRequestHandler<StockInCommand>,
    IRequestHandler<StockOutCommand>,
    IRequestHandler<AdjustStockCommand>
{
    public Task Handle(StockInCommand request, CancellationToken ct)
        => ApplyAsync(request.ItemId, request.Data.WarehouseId, request.Data.Quantity,
            InventoryTransactionType.StockIn, request.Data.Reason, request.Data.Reference, ct);

    public Task Handle(StockOutCommand request, CancellationToken ct)
        => ApplyAsync(request.ItemId, request.Data.WarehouseId, -request.Data.Quantity,
            InventoryTransactionType.StockOut, request.Data.Reason, request.Data.Reference, ct);

    public Task Handle(AdjustStockCommand request, CancellationToken ct)
        => ApplyAsync(request.ItemId, request.Data.WarehouseId, request.Data.QuantityChange,
            InventoryTransactionType.Adjustment, request.Data.Reason, reference: null, ct);

    private async Task ApplyAsync(
        int itemId, int warehouseId, int quantityChange,
        InventoryTransactionType type, string? reason, string? reference, CancellationToken ct)
    {
        var item = await inventory.GetItemByIdAsync(itemId, ct)
            ?? throw new NotFoundException(nameof(InventoryItem), itemId);

        var applied = await inventory.TryApplyMovementAsync(new InventoryTransaction
        {
            ItemId = itemId,
            WarehouseId = warehouseId,
            Type = type,
            QuantityChange = quantityChange,
            Reason = reason?.Trim(),
            Reference = reference?.Trim(),
            PerformedAtUtc = clock.UtcNow,
            PerformedBy = currentUser.UserName,
        }, ct);

        if (!applied)
        {
            throw new ConflictException(
                $"Insufficient stock of '{item.Name}' in the selected warehouse for this movement.");
        }
    }
}
