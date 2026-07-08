using EMS.Application.Assets;
using EMS.Domain.Repositories;
using EMS.Shared.Enums;
using FluentAssertions;

namespace EMS.Application.Tests.Assets;

public class DepreciationCalculatorTests
{
    private static AssetDepreciationState State(
        decimal cost,
        DepreciationMethod method,
        int lifeMonths,
        decimal residualRate,
        decimal postedTotal)
        => new(1, cost, new DateOnly(2024, 1, 1), method, lifeMonths, residualRate, postedTotal, false);

    [Fact]
    public void Straight_line_spreads_depreciable_base_evenly()
    {
        // 36 000 cost, 10% residual → base 32 400 over 36 months = 900/month.
        var state = State(36_000m, DepreciationMethod.StraightLine, 36, 0.10m, postedTotal: 0m);

        DepreciationCalculator.MonthlyAmount(state).Should().Be(900m);
    }

    [Fact]
    public void Amount_is_capped_at_the_residual_floor()
    {
        // Floor is 3 600; book value 3 900 leaves only 300 of headroom.
        var state = State(36_000m, DepreciationMethod.StraightLine, 36, 0.10m, postedTotal: 32_100m);

        DepreciationCalculator.MonthlyAmount(state).Should().Be(300m);
    }

    [Fact]
    public void Fully_depreciated_asset_yields_zero()
    {
        var state = State(36_000m, DepreciationMethod.StraightLine, 36, 0.10m, postedTotal: 32_400m);

        DepreciationCalculator.MonthlyAmount(state).Should().Be(0m);
    }

    [Fact]
    public void Declining_balance_applies_double_rate_to_book_value()
    {
        // 84-month life → monthly rate 2/84; book value 100 000 → 2 380.95.
        var state = State(100_000m, DepreciationMethod.DecliningBalance, 84, 0.10m, postedTotal: 0m);

        DepreciationCalculator.MonthlyAmount(state).Should().Be(2_380.95m);
    }

    [Fact]
    public void Declining_balance_shrinks_as_book_value_falls()
    {
        var fresh = State(100_000m, DepreciationMethod.DecliningBalance, 84, 0.10m, postedTotal: 0m);
        var later = State(100_000m, DepreciationMethod.DecliningBalance, 84, 0.10m, postedTotal: 50_000m);

        DepreciationCalculator.MonthlyAmount(later)
            .Should().BeLessThan(DepreciationCalculator.MonthlyAmount(fresh));
    }
}
