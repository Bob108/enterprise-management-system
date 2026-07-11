using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Procurement documents are permanent history, so queries use IgnoreQueryFilters:
/// a soft-deleted department/supplier/item must never hide or null-out an old document.
/// </summary>
public sealed class ProcurementRepository(EmsDbContext context) : IProcurementRepository
{
    public async Task<(IReadOnlyList<PurchaseRequest> Items, int TotalCount)> SearchRequestsAsync(
        PurchaseRequestSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = context.Set<PurchaseRequest>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(r => r.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search;
            query = query.Where(r =>
                r.RequestNumber.Contains(term) || r.RequestedByName.Contains(term));
        }

        if (criteria.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        if (criteria.RequesterUserId is { } requester)
        {
            query = query.Where(r => r.RequestedByUserId == requester);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<PurchaseRequest?> GetRequestByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<PurchaseRequest>()
            .IgnoreQueryFilters()
            .Include(r => r.Department)
            .Include(r => r.Lines).ThenInclude(l => l.AssetCategory)
            .Include(r => r.Lines).ThenInclude(l => l.InventoryItem)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<string> GetNextRequestNumberAsync(CancellationToken cancellationToken = default)
        => GetNextNumberAsync<PurchaseRequest>("PR", r => r.RequestNumber, cancellationToken);

    public void AddRequest(PurchaseRequest request) => context.Add(request);

    public void SetOriginalRowVersion(PurchaseRequest request, byte[] rowVersion)
        => context.Entry(request).Property(r => r.RowVersion).OriginalValue = rowVersion;

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> SearchOrdersAsync(
        PurchaseOrderSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = context.Set<PurchaseOrder>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(o => o.Supplier)
            .Include(o => o.PurchaseRequest)
            .Include(o => o.Lines)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search;
            query = query.Where(o =>
                o.OrderNumber.Contains(term)
                || o.Supplier.Name.Contains(term)
                || o.PurchaseRequest.RequestNumber.Contains(term));
        }

        if (criteria.Status is { } status)
        {
            query = query.Where(o => o.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<PurchaseOrder?> GetOrderByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<PurchaseOrder>()
            .IgnoreQueryFilters()
            .Include(o => o.Supplier)
            .Include(o => o.PurchaseRequest).ThenInclude(r => r.Department)
            .Include(o => o.Lines).ThenInclude(l => l.InventoryItem)
            .Include(o => o.GoodsReceivedNotes).ThenInclude(g => g.Warehouse)
            .Include(o => o.GoodsReceivedNotes).ThenInclude(g => g.Lines)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<string> GetNextOrderNumberAsync(CancellationToken cancellationToken = default)
        => GetNextNumberAsync<PurchaseOrder>("PO", o => o.OrderNumber, cancellationToken);

    public Task<string> GetNextGrnNumberAsync(CancellationToken cancellationToken = default)
        => GetNextNumberAsync<GoodsReceivedNote>("GRN", g => g.GrnNumber, cancellationToken);

    public void AddOrder(PurchaseOrder order) => context.Add(order);

    public void SetOriginalRowVersion(PurchaseOrder order, byte[] rowVersion)
        => context.Entry(order).Property(o => o.RowVersion).OriginalValue = rowVersion;

    private async Task<string> GetNextNumberAsync<TEntity>(
        string prefix,
        System.Linq.Expressions.Expression<Func<TEntity, string>> numberSelector,
        CancellationToken cancellationToken)
        where TEntity : Domain.Common.BaseEntity
    {
        var last = await context.Set<TEntity>()
            .IgnoreQueryFilters()
            .OrderByDescending(e => e.Id)
            .Select(numberSelector)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last is not null && last.StartsWith(prefix + "-") && int.TryParse(last[(prefix.Length + 1)..], out var n))
        {
            next = n + 1;
        }

        return $"{prefix}-{next:D4}";
    }
}
