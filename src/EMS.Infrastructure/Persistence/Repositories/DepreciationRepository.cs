using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using EMS.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence.Repositories;

public sealed class DepreciationRepository(EmsDbContext context) : IDepreciationRepository
{
    public async Task<IReadOnlyList<AssetDepreciationState>> GetStateForMonthAsync(
        int year, int month, CancellationToken cancellationToken = default)
    {
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        return await context.Set<Asset>()
            .AsNoTracking()
            .Where(a => a.Status != AssetStatus.Retired && a.PurchaseDate <= monthEnd)
            .Select(a => new AssetDepreciationState(
                a.Id,
                a.PurchaseCost,
                a.PurchaseDate,
                a.Category.Method,
                a.Category.UsefulLifeMonths,
                a.Category.ResidualRate,
                a.DepreciationEntries.Sum(e => (decimal?)e.Amount) ?? 0m,
                a.DepreciationEntries.Any(e => e.Year == year && e.Month == month)))
            .ToListAsync(cancellationToken);
    }

    public async Task<DateOnly?> GetEarliestPurchaseDateAsync(CancellationToken cancellationToken = default)
        => await context.Set<Asset>()
            .OrderBy(a => a.PurchaseDate)
            .Select(a => (DateOnly?)a.PurchaseDate)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddEntries(IEnumerable<DepreciationEntry> entries) => context.AddRange(entries);
}
