using EMS.Domain.Entities;
using EMS.Shared.Enums;

namespace EMS.Domain.Repositories;

/// <summary>Everything the monthly posting needs about one asset, in one query.</summary>
public sealed record AssetDepreciationState(
    int AssetId,
    decimal PurchaseCost,
    DateOnly PurchaseDate,
    DepreciationMethod Method,
    int UsefulLifeMonths,
    decimal ResidualRate,
    decimal PostedTotal,
    bool AlreadyPostedThisMonth);

public interface IDepreciationRepository
{
    /// <summary>All non-retired assets purchased on or before the end of the given month.</summary>
    Task<IReadOnlyList<AssetDepreciationState>> GetStateForMonthAsync(
        int year, int month, CancellationToken cancellationToken = default);

    /// <summary>Earliest purchase month across all assets — start of the catch-up range.</summary>
    Task<DateOnly?> GetEarliestPurchaseDateAsync(CancellationToken cancellationToken = default);

    void AddEntries(IEnumerable<DepreciationEntry> entries);
}
