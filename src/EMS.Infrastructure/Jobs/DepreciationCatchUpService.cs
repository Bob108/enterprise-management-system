using EMS.Application.Assets;
using EMS.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMS.Infrastructure.Jobs;

/// <summary>
/// Background job (design §5.5): once a day, posts depreciation for every closed month
/// from the earliest asset purchase up to last month. Each month's posting is idempotent,
/// so restarts and overlaps are harmless.
/// </summary>
public sealed class DepreciationCatchUpService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DepreciationCatchUpService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Integration tests disable the job so explicit posting tests are deterministic.
        if (!configuration.GetValue("Jobs:DepreciationCatchUpEnabled", defaultValue: true))
        {
            return;
        }

        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CatchUpAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Depreciation catch-up run failed; retrying next cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CatchUpAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDepreciationRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var earliest = await repository.GetEarliestPurchaseDateAsync(cancellationToken);
        if (earliest is null)
        {
            return; // no assets yet
        }

        // Post every fully closed month up to and including last month.
        var cursor = new DateOnly(earliest.Value.Year, earliest.Value.Month, 1);
        var lastClosed = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1);
        var posted = 0;

        while (new DateOnly(cursor.Year, cursor.Month, 1) <= new DateOnly(lastClosed.Year, lastClosed.Month, 1))
        {
            posted += await sender.Send(
                new PostMonthlyDepreciationCommand(cursor.Year, cursor.Month), cancellationToken);
            cursor = cursor.AddMonths(1);
        }

        if (posted > 0)
        {
            logger.LogInformation("Depreciation catch-up posted {Count} entries", posted);
        }
    }
}
