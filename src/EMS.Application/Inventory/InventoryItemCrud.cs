using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Inventory;
using FluentValidation;
using MediatR;

namespace EMS.Application.Inventory;

// Catalog maintenance uses the inventory.adjust permission — the Store Keeper's remit.

[RequiresPermission(Permissions.Inventory.Adjust)]
public sealed record CreateInventoryItemCommand(CreateInventoryItemRequest Data) : IRequest<int>;

[RequiresPermission(Permissions.Inventory.Adjust)]
public sealed record UpdateInventoryItemCommand(int Id, UpdateInventoryItemRequest Data) : IRequest;

[RequiresPermission(Permissions.Inventory.Adjust)]
public sealed record DeleteInventoryItemCommand(int Id) : IRequest;

internal sealed class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemCommandValidator(IInventoryRepository inventory, IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Data.Name).NotEmpty().MaximumLength(200).OverridePropertyName("Name");
        RuleFor(x => x.Data.Category).MaximumLength(100).OverridePropertyName("Category");
        RuleFor(x => x.Data.Unit).NotEmpty().MaximumLength(16).OverridePropertyName("Unit");
        RuleFor(x => x.Data.Description).MaximumLength(500).OverridePropertyName("Description");

        RuleFor(x => x.Data.Name)
            .MustAsync(async (name, ct) => !await inventory.ItemNameTakenAsync(name, null, ct))
            .WithMessage("An item with this name already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name));

        AddMinimumRules(this, warehouses, x => x.Data.Minimums);
    }

    internal static void AddMinimumRules<T>(
        AbstractValidator<T> validator,
        IWarehouseRepository warehouses,
        Func<T, List<WarehouseMinimum>> selector)
    {
        validator.RuleFor(x => selector(x))
            .Must(m => m.Select(w => w.WarehouseId).Distinct().Count() == m.Count)
            .WithMessage("Duplicate warehouse in minimum levels.")
            .OverridePropertyName("Minimums");
        validator.RuleForEach(x => selector(x)).ChildRules(m =>
        {
            m.RuleFor(w => w.MinimumQuantity).GreaterThanOrEqualTo(0);
            m.RuleFor(w => w.WarehouseId)
                .MustAsync(warehouses.ExistsAsync)
                .WithMessage("Warehouse does not exist.");
        }).OverridePropertyName("Minimums");
    }
}

internal sealed class UpdateInventoryItemCommandValidator : AbstractValidator<UpdateInventoryItemCommand>
{
    public UpdateInventoryItemCommandValidator(IInventoryRepository inventory, IWarehouseRepository warehouses)
    {
        RuleFor(x => x.Data.Name).NotEmpty().MaximumLength(200).OverridePropertyName("Name");
        RuleFor(x => x.Data.Category).MaximumLength(100).OverridePropertyName("Category");
        RuleFor(x => x.Data.Unit).NotEmpty().MaximumLength(16).OverridePropertyName("Unit");
        RuleFor(x => x.Data.Description).MaximumLength(500).OverridePropertyName("Description");
        RuleFor(x => x.Data.RowVersion).NotEmpty().OverridePropertyName("RowVersion");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await inventory.ItemNameTakenAsync(cmd.Data.Name, cmd.Id, ct))
            .WithMessage("An item with this name already exists.")
            .OverridePropertyName("Name")
            .When(x => !string.IsNullOrWhiteSpace(x.Data.Name));

        CreateInventoryItemCommandValidator.AddMinimumRules(this, warehouses, x => x.Data.Minimums);
    }
}

internal sealed class InventoryItemCrudHandlers(
    IInventoryRepository inventory,
    IUnitOfWork unitOfWork) :
    IRequestHandler<CreateInventoryItemCommand, int>,
    IRequestHandler<UpdateInventoryItemCommand>,
    IRequestHandler<DeleteInventoryItemCommand>
{
    public async Task<int> Handle(CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var data = request.Data;
        var item = new InventoryItem
        {
            ItemCode = await inventory.GetNextItemCodeAsync(cancellationToken),
            Name = data.Name.Trim(),
            Category = data.Category?.Trim(),
            Unit = data.Unit.Trim(),
            Description = data.Description?.Trim(),
            StockLevels = data.Minimums
                .Select(m => new StockLevel { WarehouseId = m.WarehouseId, MinimumQuantity = m.MinimumQuantity })
                .ToList(),
        };

        inventory.AddItem(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public async Task Handle(UpdateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = await inventory.GetItemByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), request.Id);

        var data = request.Data;
        inventory.SetOriginalRowVersion(item, Convert.FromBase64String(data.RowVersion));

        item.Name = data.Name.Trim();
        item.Category = data.Category?.Trim();
        item.Unit = data.Unit.Trim();
        item.Description = data.Description?.Trim();

        // Minimums are upserted; stock-level rows are never removed here — they carry balances.
        foreach (var minimum in data.Minimums)
        {
            var level = item.StockLevels.FirstOrDefault(s => s.WarehouseId == minimum.WarehouseId);
            if (level is null)
            {
                item.StockLevels.Add(new StockLevel
                {
                    WarehouseId = minimum.WarehouseId,
                    MinimumQuantity = minimum.MinimumQuantity,
                });
            }
            else
            {
                level.MinimumQuantity = minimum.MinimumQuantity;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = await inventory.GetItemByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryItem), request.Id);

        var onHand = await inventory.GetOnHandTotalAsync(request.Id, cancellationToken);
        if (onHand > 0)
        {
            throw new ConflictException(
                $"'{item.Name}' still has {onHand} {item.Unit} on hand and cannot be deleted.");
        }

        inventory.RemoveItem(item); // soft delete via audit interceptor
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
