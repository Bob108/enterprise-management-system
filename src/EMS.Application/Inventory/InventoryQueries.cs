using EMS.Application.Common.Exceptions;
using EMS.Application.Common.Security;
using EMS.Domain.Repositories;
using EMS.Shared.Authorization;
using EMS.Shared.Common;
using EMS.Shared.Inventory;
using MediatR;

namespace EMS.Application.Inventory;

[RequiresPermission(Permissions.Inventory.View)]
public sealed record GetInventoryItemsQuery(
    string? Search,
    bool LowStockOnly = false,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<InventoryItemListDto>>;

[RequiresPermission(Permissions.Inventory.View)]
public sealed record GetInventoryItemByIdQuery(int Id) : IRequest<InventoryItemDetailDto>;

[RequiresPermission(Permissions.Inventory.View)]
public sealed record GetItemTransactionsQuery(int ItemId, int Take = 50)
    : IRequest<IReadOnlyList<InventoryTransactionDto>>;

internal sealed class InventoryQueryHandlers(IInventoryRepository inventory) :
    IRequestHandler<GetInventoryItemsQuery, PagedResult<InventoryItemListDto>>,
    IRequestHandler<GetInventoryItemByIdQuery, InventoryItemDetailDto>,
    IRequestHandler<GetItemTransactionsQuery, IReadOnlyList<InventoryTransactionDto>>
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<InventoryItemListDto>> Handle(
        GetInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var (rows, totalCount) = await inventory.SearchItemsAsync(
            new InventoryItemSearchCriteria(request.Search?.Trim(), request.LowStockOnly, page, pageSize),
            cancellationToken);

        var items = rows
            .Select(r => new InventoryItemListDto(
                r.Item.Id, r.Item.ItemCode, r.Item.Name, r.Item.Category, r.Item.Unit,
                r.TotalOnHand, r.TotalMinimum, r.IsBelowMinimum, r.WarehouseCount))
            .ToList();

        return new PagedResult<InventoryItemListDto>(items, page, pageSize, totalCount);
    }

    public async Task<InventoryItemDetailDto> Handle(
        GetInventoryItemByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await inventory.GetItemByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.InventoryItem), request.Id);

        return new InventoryItemDetailDto(
            item.Id,
            item.ItemCode,
            item.Name,
            item.Category,
            item.Unit,
            item.Description,
            item.StockLevels
                .OrderBy(s => s.Warehouse.Name)
                .Select(s => new StockLevelDto(s.WarehouseId, s.Warehouse.Name, s.Quantity, s.MinimumQuantity))
                .ToList(),
            Convert.ToBase64String(item.RowVersion));
    }

    public async Task<IReadOnlyList<InventoryTransactionDto>> Handle(
        GetItemTransactionsQuery request, CancellationToken cancellationToken)
    {
        var transactions = await inventory.GetTransactionsAsync(
            request.ItemId, Math.Clamp(request.Take, 1, 200), cancellationToken);

        return transactions
            .Select(t => new InventoryTransactionDto(
                t.Id, t.Type, t.Warehouse.Name, t.QuantityChange,
                t.Reason, t.Reference, t.PerformedAtUtc, t.PerformedBy))
            .ToList();
    }
}
