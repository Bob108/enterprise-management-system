using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Shared.Enums;
using FluentAssertions;

namespace EMS.Domain.Tests.Entities;

/// <summary>
/// The asset status machine (design §6.2) — every transition table row becomes a test.
/// </summary>
public class AssetStateMachineTests
{
    private static readonly DateOnly Today = new(2026, 7, 8);

    private static Asset NewAsset(AssetStatus status = AssetStatus.Available) => new()
    {
        AssetCode = "ITE-0001",
        Name = "Test Laptop",
        Status = status,
        DepartmentId = 1,
        PurchaseCost = 100_000m,
    };

    [Fact]
    public void Assign_from_available_sets_status_and_opens_history_row()
    {
        var asset = NewAsset();

        var assignment = asset.AssignTo(42, "New in box", Today);

        asset.Status.Should().Be(AssetStatus.Assigned);
        asset.CurrentAssigneeEmployeeId.Should().Be(42);
        assignment.EmployeeId.Should().Be(42);
        assignment.ReturnedOn.Should().BeNull();
        asset.Assignments.Should().ContainSingle();
    }

    [Theory]
    [InlineData(AssetStatus.Assigned)]
    [InlineData(AssetStatus.UnderRepair)]
    [InlineData(AssetStatus.Lost)]
    [InlineData(AssetStatus.Retired)]
    public void Assign_from_any_other_status_throws(AssetStatus status)
    {
        var asset = NewAsset(status);

        var act = () => asset.AssignTo(42, null, Today);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Return_closes_open_assignment_and_frees_asset()
    {
        var asset = NewAsset();
        asset.AssignTo(42, "out", Today);

        asset.Return("scratched lid", Today.AddDays(30));

        asset.Status.Should().Be(AssetStatus.Available);
        asset.CurrentAssigneeEmployeeId.Should().BeNull();
        asset.Assignments.Single().ReturnedOn.Should().Be(Today.AddDays(30));
        asset.Assignments.Single().ConditionIn.Should().Be("scratched lid");
    }

    [Fact]
    public void Return_when_not_assigned_throws()
    {
        var act = () => NewAsset().Return(null, Today);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Repair_cycle_returns_to_assignee_when_one_is_on_record()
    {
        var asset = NewAsset();
        asset.AssignTo(42, null, Today);

        asset.MarkUnderRepair();
        asset.Status.Should().Be(AssetStatus.UnderRepair);

        asset.MarkRepaired();
        asset.Status.Should().Be(AssetStatus.Assigned);
        asset.CurrentAssigneeEmployeeId.Should().Be(42);
    }

    [Fact]
    public void Repair_cycle_returns_to_available_when_unassigned()
    {
        var asset = NewAsset();

        asset.MarkUnderRepair();
        asset.MarkRepaired();

        asset.Status.Should().Be(AssetStatus.Available);
    }

    [Fact]
    public void Report_lost_closes_open_assignment_and_clears_assignee()
    {
        var asset = NewAsset();
        asset.AssignTo(42, null, Today);

        asset.ReportLost(Today.AddDays(10));

        asset.Status.Should().Be(AssetStatus.Lost);
        asset.CurrentAssigneeEmployeeId.Should().BeNull();
        asset.Assignments.Single().ReturnedOn.Should().Be(Today.AddDays(10));
        asset.Assignments.Single().ConditionIn.Should().Be("Reported lost");
    }

    [Fact]
    public void Recover_is_only_valid_from_lost()
    {
        var lost = NewAsset(AssetStatus.Lost);
        lost.Recover();
        lost.Status.Should().Be(AssetStatus.Available);

        var available = NewAsset();
        var act = () => available.Recover();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Transfer_moves_department_and_records_history()
    {
        var asset = NewAsset();
        asset.DepartmentId = 1;

        asset.TransferTo(2, "Reorganisation", Today);

        asset.DepartmentId.Should().Be(2);
        var transfer = asset.Transfers.Single();
        transfer.FromDepartmentId.Should().Be(1);
        transfer.ToDepartmentId.Should().Be(2);
    }

    [Fact]
    public void Transfer_to_same_department_throws()
    {
        var asset = NewAsset();
        asset.DepartmentId = 1;

        var act = () => asset.TransferTo(1, null, Today);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(AssetStatus.Lost)]
    [InlineData(AssetStatus.Retired)]
    public void Transfer_is_blocked_for_lost_and_retired(AssetStatus status)
    {
        var asset = NewAsset(status);

        var act = () => asset.TransferTo(2, null, Today);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Dispose_from_available_computes_gain_loss_and_is_terminal()
    {
        var asset = NewAsset();

        var disposal = asset.Dispose(DisposalMethod.Sold, proceeds: 30_000m, bookValue: 40_000m, "EOL", Today);

        asset.Status.Should().Be(AssetStatus.Retired);
        disposal.GainLoss.Should().Be(-10_000m); // sold below book value → loss
    }

    [Theory]
    [InlineData(AssetStatus.Assigned)]
    [InlineData(AssetStatus.UnderRepair)]
    [InlineData(AssetStatus.Retired)]
    public void Dispose_requires_available_or_lost(AssetStatus status)
    {
        var asset = NewAsset(status);

        var act = () => asset.Dispose(DisposalMethod.Scrapped, null, 0m, null, Today);

        act.Should().Throw<DomainException>();
    }
}
