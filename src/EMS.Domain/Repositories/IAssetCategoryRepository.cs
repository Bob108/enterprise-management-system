using EMS.Domain.Entities;

namespace EMS.Domain.Repositories;

public interface IAssetCategoryRepository
{
    Task<IReadOnlyList<(AssetCategory Category, int AssetCount)>> GetAllWithAssetCountAsync(
        CancellationToken cancellationToken = default);

    Task<AssetCategory?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> NameOrPrefixTakenAsync(
        string name, string codePrefix, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> CountAssetsAsync(int categoryId, CancellationToken cancellationToken = default);

    void Add(AssetCategory category);

    void Remove(AssetCategory category);
}

public interface ISupplierRepository
{
    Task<IReadOnlyList<(Supplier Supplier, int AssetCount)>> GetAllWithAssetCountAsync(
        CancellationToken cancellationToken = default);

    Task<Supplier?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> NameTakenAsync(string name, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> CountAssetsAsync(int supplierId, CancellationToken cancellationToken = default);

    void Add(Supplier supplier);

    void Remove(Supplier supplier);
}
