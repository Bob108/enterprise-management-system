using EMS.Domain.Entities;
using EMS.Shared.Enums;

namespace EMS.Domain.Repositories;

public sealed record AssetSearchCriteria(
    string? Search,
    int? CategoryId,
    int? DepartmentId,
    AssetStatus? Status,
    int Page,
    int PageSize);

/// <summary>List row: entity plus values that require aggregation (depreciated total).</summary>
public sealed record AssetListRow(Asset Asset, decimal DepreciatedTotal);

public interface IAssetRepository
{
    /// <summary>Read-only, with Category/Department/CurrentAssignee loaded.</summary>
    Task<(IReadOnlyList<AssetListRow> Items, int TotalCount)> SearchAsync(
        AssetSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Tracked, with all references and histories loaded.</summary>
    Task<Asset?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<decimal> GetDepreciatedTotalAsync(int assetId, CancellationToken cancellationToken = default);

    /// <summary>Next code in the {prefix}-#### sequence (unique index is the race backstop).</summary>
    Task<string> GetNextAssetCodeAsync(string codePrefix, CancellationToken cancellationToken = default);

    void Add(Asset asset);

    void Remove(Asset asset);

    void SetOriginalRowVersion(Asset asset, byte[] rowVersion);
}
