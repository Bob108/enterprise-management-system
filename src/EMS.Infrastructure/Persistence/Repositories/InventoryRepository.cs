using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence.Repositories;

public sealed class InventoryRepository(EmsDbContext context) : IInventoryRepository
{
    public async Task<(IReadOnlyList<InventoryItemRow> Items, int TotalCount)> SearchItemsAsync(
        InventoryItemSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = context.Set<InventoryItem>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search;
            query = query.Where(i =>
                i.ItemCode.Contains(term)
                || i.Name.Contains(term)
                || (i.Category != null && i.Category.Contains(term)));
        }

        if (criteria.LowStockOnly)
        {
            query = query.Where(i => i.StockLevels.Any(s => s.Quantity < s.MinimumQuantity));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(i => i.Name)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(i => new
            {
                Item = i,
                OnHand = i.StockLevels.Sum(s => (int?)s.Quantity) ?? 0,
                Minimum = i.StockLevels.Sum(s => (int?)s.MinimumQuantity) ?? 0,
                Below = i.StockLevels.Any(s => s.Quantity < s.MinimumQuantity),
                WarehouseCount = i.StockLevels.Count,
            })
            .ToListAsync(cancellationToken);

        return (rows
            .Select(r => new InventoryItemRow(r.Item, r.OnHand, r.Minimum, r.Below, r.WarehouseCount))
            .ToList(), totalCount);
    }

    public Task<InventoryItem?> GetItemByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<InventoryItem>()
            .Include(i => i.StockLevels).ThenInclude(s => s.Warehouse)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<bool> ItemNameTakenAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
        => context.Set<InventoryItem>().AnyAsync(
            i => i.Name == name && (excludeId == null || i.Id != excludeId), cancellationToken);

    public async Task<string> GetNextItemCodeAsync(CancellationToken cancellationToken = default)
    {
        var last = await context.Set<InventoryItem>()
            .IgnoreQueryFilters()
            .OrderByDescending(i => i.Id)
            .Select(i => i.ItemCode)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last is not null && last.StartsWith("INV-") && int.TryParse(last[4..], out var n))
        {
            next = n + 1;
        }

        return $"INV-{next:D4}";
    }

    public Task<int> GetOnHandTotalAsync(int itemId, CancellationToken cancellationToken = default)
        => context.Set<StockLevel>()
            .Where(s => s.ItemId == itemId)
            .SumAsync(s => (int?)s.Quantity ?? 0, cancellationToken);

    public void AddItem(InventoryItem item) => context.Add(item);

    public void RemoveItem(InventoryItem item) => context.Remove(item);

    public void SetOriginalRowVersion(InventoryItem item, byte[] rowVersion)
        => context.Entry(item).Property(i => i.RowVersion).OriginalValue = rowVersion;

    /// <inheritdoc />
    public async Task<bool> TryApplyMovementAsync(
        InventoryTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var dbTransaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Ensure the balance row exists (items are stocked lazily per warehouse).
        var levelExists = await context.Set<StockLevel>().AnyAsync(
            s => s.ItemId == transaction.ItemId && s.WarehouseId == transaction.WarehouseId,
            cancellationToken);
        if (!levelExists)
        {
            context.Add(new StockLevel
            {
                ItemId = transaction.ItemId,
                WarehouseId = transaction.WarehouseId,
                Quantity = 0,
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        // The atomic guard (design §7.3): a single conditional UPDATE that refuses to take
        // the balance negative. Two concurrent stock-outs of the last unit cannot both
        // match the WHERE clause — one succeeds, the other affects zero rows.
        var affected = await context.Set<StockLevel>()
            .Where(s => s.ItemId == transaction.ItemId
                && s.WarehouseId == transaction.WarehouseId
                && s.Quantity + transaction.QuantityChange >= 0)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.Quantity, s => s.Quantity + transaction.QuantityChange),
                cancellationToken);

        if (affected == 0)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            return false;
        }

        context.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<InventoryTransaction>> GetTransactionsAsync(
        int itemId, int take, CancellationToken cancellationToken = default)
        => await context.Set<InventoryTransaction>()
            .AsNoTracking()
            .Include(t => t.Warehouse)
            .Where(t => t.ItemId == itemId)
            .OrderByDescending(t => t.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}

public sealed class WarehouseRepository(EmsDbContext context) : IWarehouseRepository
{
    public async Task<IReadOnlyList<(Warehouse Warehouse, int StockedItemCount)>> GetAllWithItemCountAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Set<Warehouse>()
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .Select(w => new { Warehouse = w, Count = w.StockLevels.Count(s => s.Quantity > 0) })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Warehouse, r.Count)).ToList();
    }

    public Task<Warehouse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Warehouse>().SingleOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Warehouse>().AnyAsync(w => w.Id == id, cancellationToken);

    public Task<bool> NameOrCodeTakenAsync(
        string name, string code, int? excludeId, CancellationToken cancellationToken = default)
        => context.Set<Warehouse>().AnyAsync(
            w => (w.Name == name || w.Code == code) && (excludeId == null || w.Id != excludeId),
            cancellationToken);

    public Task<bool> HasStockAsync(int warehouseId, CancellationToken cancellationToken = default)
        => context.Set<StockLevel>().AnyAsync(
            s => s.WarehouseId == warehouseId && s.Quantity > 0, cancellationToken);

    public void Add(Warehouse warehouse) => context.Add(warehouse);

    public void Remove(Warehouse warehouse) => context.Remove(warehouse);
}
