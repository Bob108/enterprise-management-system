using EMS.Domain.Entities;

namespace EMS.Domain.Repositories;

public sealed record InventoryItemSearchCriteria(
    string? Search,
    bool LowStockOnly,
    int Page,
    int PageSize);

public sealed record InventoryItemRow(
    InventoryItem Item,
    int TotalOnHand,
    int TotalMinimum,
    bool IsBelowMinimum,
    int WarehouseCount);

public interface IInventoryRepository
{
    Task<(IReadOnlyList<InventoryItemRow> Items, int TotalCount)> SearchItemsAsync(
        InventoryItemSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Tracked, with stock levels and their warehouses loaded.</summary>
    Task<InventoryItem?> GetItemByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ItemNameTakenAsync(string name, int? excludeId, CancellationToken cancellationToken = default);

    Task<string> GetNextItemCodeAsync(CancellationToken cancellationToken = default);

    Task<int> GetOnHandTotalAsync(int itemId, CancellationToken cancellationToken = default);

    void AddItem(InventoryItem item);

    void RemoveItem(InventoryItem item);

    void SetOriginalRowVersion(InventoryItem item, byte[] rowVersion);

    /// <summary>
    /// Atomically applies a signed movement: conditional UPDATE that refuses to take the
    /// balance below zero, plus the ledger row, in one database transaction (design §7.3).
    /// Returns false when there is insufficient stock — never throws for that case.
    /// </summary>
    Task<bool> TryApplyMovementAsync(InventoryTransaction transaction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryTransaction>> GetTransactionsAsync(
        int itemId, int take, CancellationToken cancellationToken = default);
}

public interface IWarehouseRepository
{
    Task<IReadOnlyList<(Warehouse Warehouse, int StockedItemCount)>> GetAllWithItemCountAsync(
        CancellationToken cancellationToken = default);

    Task<Warehouse?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> NameOrCodeTakenAsync(
        string name, string code, int? excludeId, CancellationToken cancellationToken = default);

    /// <summary>True when any stock remains — such a warehouse cannot be deleted.</summary>
    Task<bool> HasStockAsync(int warehouseId, CancellationToken cancellationToken = default);

    void Add(Warehouse warehouse);

    void Remove(Warehouse warehouse);
}
