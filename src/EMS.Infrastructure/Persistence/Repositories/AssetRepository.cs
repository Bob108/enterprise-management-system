using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence.Repositories;

public sealed class AssetRepository(EmsDbContext context) : IAssetRepository
{
    public async Task<(IReadOnlyList<AssetListRow> Items, int TotalCount)> SearchAsync(
        AssetSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = context.Set<Asset>()
            .AsNoTracking()
            .Include(a => a.Category)
            .Include(a => a.Department)
            .Include(a => a.CurrentAssignee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search;
            query = query.Where(a =>
                a.AssetCode.Contains(term)
                || a.Name.Contains(term)
                || (a.SerialNumber != null && a.SerialNumber.Contains(term)));
        }

        if (criteria.CategoryId is { } categoryId)
        {
            query = query.Where(a => a.CategoryId == categoryId);
        }

        if (criteria.DepartmentId is { } departmentId)
        {
            query = query.Where(a => a.DepartmentId == departmentId);
        }

        if (criteria.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(a => a.AssetCode)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(a => new { Asset = a, Depreciated = a.DepreciationEntries.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        return (rows.Select(r => new AssetListRow(r.Asset, r.Depreciated)).ToList(), totalCount);
    }

    public Task<Asset?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Asset>()
            .Include(a => a.Category)
            .Include(a => a.Department)
            .Include(a => a.Supplier)
            .Include(a => a.CurrentAssignee)
            .Include(a => a.Assignments).ThenInclude(x => x.Employee)
            .Include(a => a.Transfers)
            .Include(a => a.Disposal)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<decimal> GetDepreciatedTotalAsync(int assetId, CancellationToken cancellationToken = default)
        => context.Set<DepreciationEntry>()
            .Where(e => e.AssetId == assetId)
            .SumAsync(e => e.Amount, cancellationToken);

    public async Task<string> GetNextAssetCodeAsync(
        string codePrefix, CancellationToken cancellationToken = default)
    {
        // Includes soft-deleted rows: codes are never reused. The unique index is the
        // backstop for the (rare) concurrent-register race.
        var prefix = codePrefix + "-";
        var last = await context.Set<Asset>()
            .IgnoreQueryFilters()
            .Where(a => a.AssetCode.StartsWith(prefix))
            .OrderByDescending(a => a.AssetCode)
            .Select(a => a.AssetCode)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last is not null && int.TryParse(last[prefix.Length..], out var n))
        {
            next = n + 1;
        }

        return $"{prefix}{next:D4}";
    }

    public void Add(Asset asset) => context.Add(asset);

    public void Remove(Asset asset) => context.Remove(asset);

    public void SetOriginalRowVersion(Asset asset, byte[] rowVersion)
        => context.Entry(asset).Property(a => a.RowVersion).OriginalValue = rowVersion;
}
