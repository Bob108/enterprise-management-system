using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence.Repositories;

public sealed class AssetCategoryRepository(EmsDbContext context) : IAssetCategoryRepository
{
    public async Task<IReadOnlyList<(AssetCategory Category, int AssetCount)>> GetAllWithAssetCountAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Set<AssetCategory>()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { Category = c, AssetCount = c.Assets.Count })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Category, r.AssetCount)).ToList();
    }

    public Task<AssetCategory?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<AssetCategory>().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<AssetCategory>().AnyAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameOrPrefixTakenAsync(
        string name, string codePrefix, int? excludeId, CancellationToken cancellationToken = default)
        => context.Set<AssetCategory>().AnyAsync(
            c => (c.Name == name || c.CodePrefix == codePrefix) && (excludeId == null || c.Id != excludeId),
            cancellationToken);

    public Task<int> CountAssetsAsync(int categoryId, CancellationToken cancellationToken = default)
        => context.Set<Asset>().CountAsync(a => a.CategoryId == categoryId, cancellationToken);

    public void Add(AssetCategory category) => context.Add(category);

    public void Remove(AssetCategory category) => context.Remove(category);
}

public sealed class SupplierRepository(EmsDbContext context) : ISupplierRepository
{
    public async Task<IReadOnlyList<(Supplier Supplier, int AssetCount)>> GetAllWithAssetCountAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Set<Supplier>()
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new { Supplier = s, AssetCount = s.Assets.Count })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Supplier, r.AssetCount)).ToList();
    }

    public Task<Supplier?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Supplier>().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Supplier>().AnyAsync(s => s.Id == id, cancellationToken);

    public Task<bool> NameTakenAsync(string name, int? excludeId, CancellationToken cancellationToken = default)
        => context.Set<Supplier>().AnyAsync(
            s => s.Name == name && (excludeId == null || s.Id != excludeId), cancellationToken);

    public Task<int> CountAssetsAsync(int supplierId, CancellationToken cancellationToken = default)
        => context.Set<Asset>().CountAsync(a => a.SupplierId == supplierId, cancellationToken);

    public void Add(Supplier supplier) => context.Add(supplier);

    public void Remove(Supplier supplier) => context.Remove(supplier);
}
