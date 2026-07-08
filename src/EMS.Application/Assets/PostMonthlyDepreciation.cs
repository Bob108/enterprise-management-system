using EMS.Application.Common.Interfaces;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EMS.Application.Assets;

/// <summary>
/// Posts one immutable depreciation entry per eligible asset for the given month
/// (design §6.3). Idempotent: already-posted asset/months are skipped, so the catch-up
/// job and the admin endpoint can both call it safely. No [RequiresPermission] — the
/// system job runs unauthenticated; the admin endpoint enforces its own permission.
/// </summary>
public sealed record PostMonthlyDepreciationCommand(int Year, int Month) : IRequest<int>;

internal sealed class PostMonthlyDepreciationCommandValidator
    : AbstractValidator<PostMonthlyDepreciationCommand>
{
    public PostMonthlyDepreciationCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public static class DepreciationCalculator
{
    /// <summary>Monthly charge, capped so book value never drops below the residual floor.</summary>
    public static decimal MonthlyAmount(AssetDepreciationState state)
    {
        var residualFloor = Math.Round(state.PurchaseCost * state.ResidualRate, 2);
        var bookValue = state.PurchaseCost - state.PostedTotal;
        var headroom = bookValue - residualFloor;
        if (headroom <= 0 || state.UsefulLifeMonths <= 0)
        {
            return 0m;
        }

        var amount = state.Method switch
        {
            Shared.Enums.DepreciationMethod.StraightLine =>
                (state.PurchaseCost - residualFloor) / state.UsefulLifeMonths,
            // Double-declining balance: annual rate 2/lifeYears, applied monthly to book value.
            Shared.Enums.DepreciationMethod.DecliningBalance =>
                bookValue * (2m / state.UsefulLifeMonths),
            _ => 0m,
        };

        return Math.Min(Math.Round(amount, 2), headroom);
    }
}

internal sealed class PostMonthlyDepreciationCommandHandler(
    IDepreciationRepository depreciation,
    IUnitOfWork unitOfWork,
    IDateTime clock,
    ILogger<PostMonthlyDepreciationCommandHandler> logger)
    : IRequestHandler<PostMonthlyDepreciationCommand, int>
{
    public async Task<int> Handle(
        PostMonthlyDepreciationCommand request, CancellationToken cancellationToken)
    {
        var states = await depreciation.GetStateForMonthAsync(
            request.Year, request.Month, cancellationToken);

        var now = clock.UtcNow;
        var entries = new List<DepreciationEntry>();

        foreach (var state in states.Where(s => !s.AlreadyPostedThisMonth))
        {
            var amount = DepreciationCalculator.MonthlyAmount(state);
            if (amount <= 0)
            {
                continue;
            }

            entries.Add(new DepreciationEntry
            {
                AssetId = state.AssetId,
                Year = request.Year,
                Month = request.Month,
                Amount = amount,
                BookValueAfter = state.PurchaseCost - state.PostedTotal - amount,
                PostedAtUtc = now,
            });
        }

        if (entries.Count > 0)
        {
            depreciation.AddEntries(entries);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Posted {Count} depreciation entries for {Year}-{Month:D2}",
                entries.Count, request.Year, request.Month);
        }

        return entries.Count;
    }
}
