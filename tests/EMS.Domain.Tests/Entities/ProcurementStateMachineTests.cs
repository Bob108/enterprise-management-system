using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Shared.Enums;
using FluentAssertions;

namespace EMS.Domain.Tests.Entities;

public class PurchaseRequestStateMachineTests
{
    private static readonly DateTime Now = new(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
    private const decimal Threshold = 100_000m;

    private static PurchaseRequest NewRequest(decimal lineCost = 10_000m, int quantity = 1) => new()
    {
        RequestNumber = "PR-0001",
        RequestedByUserId = 1,
        RequestedByName = "Requester",
        Lines = [new PurchaseRequestLine { Description = "Thing", Quantity = quantity, EstimatedUnitCost = lineCost }],
    };

    [Fact]
    public void Submit_without_lines_throws()
    {
        var pr = NewRequest();
        pr.Lines.Clear();

        var act = () => pr.Submit(Threshold);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(99_999, false)]
    [InlineData(100_000, true)]
    public void Submit_routes_by_threshold(decimal cost, bool needsSecond)
    {
        var pr = NewRequest(cost);

        pr.Submit(Threshold);

        pr.Status.Should().Be(PurchaseRequestStatus.Submitted);
        pr.RequiresSecondApproval.Should().Be(needsSecond);
    }

    [Fact]
    public void Requester_cannot_approve_own_request()
    {
        var pr = NewRequest();
        pr.Submit(Threshold);

        var act = () => pr.Approve(approverUserId: 1, "Requester", Now);

        act.Should().Throw<DomainException>().WithMessage("*own*");
    }

    [Fact]
    public void Single_level_request_approves_on_first_signature()
    {
        var pr = NewRequest(50_000m);
        pr.Submit(Threshold);

        pr.Approve(2, "Manager", Now);

        pr.Status.Should().Be(PurchaseRequestStatus.Approved);
        pr.FirstApprovedByName.Should().Be("Manager");
    }

    [Fact]
    public void Two_level_request_stays_submitted_after_first_signature()
    {
        var pr = NewRequest(150_000m);
        pr.Submit(Threshold);

        pr.Approve(2, "Manager", Now);

        pr.Status.Should().Be(PurchaseRequestStatus.Submitted);
        pr.AwaitingSecondApproval.Should().BeTrue();

        pr.Approve(3, "Procurement Officer", Now);
        pr.Status.Should().Be(PurchaseRequestStatus.Approved);
    }

    [Fact]
    public void Second_approval_by_the_first_approver_throws()
    {
        var pr = NewRequest(150_000m);
        pr.Submit(Threshold);
        pr.Approve(2, "Manager", Now);

        var act = () => pr.Approve(2, "Manager", Now);

        act.Should().Throw<DomainException>().WithMessage("*different approver*");
    }

    [Fact]
    public void Return_to_draft_clears_approvals()
    {
        var pr = NewRequest(150_000m);
        pr.Submit(Threshold);
        pr.Approve(2, "Manager", Now);

        pr.ReturnToDraft();

        pr.Status.Should().Be(PurchaseRequestStatus.Draft);
        pr.FirstApprovedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Reject_records_reason_and_blocks_conversion()
    {
        var pr = NewRequest();
        pr.Submit(Threshold);

        pr.Reject(2, "Budget frozen");

        pr.Status.Should().Be(PurchaseRequestStatus.Rejected);
        pr.RejectionReason.Should().Be("Budget frozen");

        var act = () => pr.MarkConverted(9);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Convert_requires_approved_and_links_the_order()
    {
        var pr = NewRequest(50_000m);
        pr.Submit(Threshold);
        pr.Approve(2, "Manager", Now);

        pr.MarkConverted(42);

        pr.Status.Should().Be(PurchaseRequestStatus.Converted);
        pr.PurchaseOrderId.Should().Be(42);
    }
}

public class PurchaseOrderStateMachineTests
{
    private static readonly DateTime Now = new(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);

    private static PurchaseOrder NewIssuedOrder(int orderedQuantity = 10)
    {
        var order = new PurchaseOrder
        {
            OrderNumber = "PO-0001",
            Lines =
            [
                new PurchaseOrderLine
                {
                    Id = 1, Description = "Widget", Nature = ItemNature.Consumable,
                    OrderedQuantity = orderedQuantity, UnitPrice = 100m,
                },
            ],
        };
        order.Issue(Now);
        return order;
    }

    [Fact]
    public void Receive_accumulates_and_transitions_to_partially_then_fully()
    {
        var order = NewIssuedOrder(10);

        order.Receive("GRN-0001", 1, [(1, 4)], Now, "keeper", null);
        order.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        order.Lines[0].ReceivedQuantity.Should().Be(4);

        order.Receive("GRN-0002", 1, [(1, 6)], Now, "keeper", null);
        order.Status.Should().Be(PurchaseOrderStatus.FullyReceived);
        order.GoodsReceivedNotes.Should().HaveCount(2);
    }

    [Fact]
    public void Over_receiving_throws_and_changes_nothing()
    {
        var order = NewIssuedOrder(10);
        order.Receive("GRN-0001", 1, [(1, 4)], Now, "keeper", null);

        var act = () => order.Receive("GRN-0002", 1, [(1, 7)], Now, "keeper", null);

        act.Should().Throw<DomainException>().WithMessage("*outstanding*");
        order.Lines[0].ReceivedQuantity.Should().Be(4);
    }

    [Fact]
    public void Cancel_is_blocked_once_goods_are_received()
    {
        var order = NewIssuedOrder(10);
        order.Receive("GRN-0001", 1, [(1, 1)], Now, "keeper", null);

        var act = order.Cancel;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Close_requires_fully_received()
    {
        var order = NewIssuedOrder(2);

        var early = order.Close;
        early.Should().Throw<DomainException>();

        order.Receive("GRN-0001", 1, [(1, 2)], Now, "keeper", null);
        order.Close();
        order.Status.Should().Be(PurchaseOrderStatus.Closed);
    }

    [Fact]
    public void Receive_on_draft_order_throws()
    {
        var order = new PurchaseOrder
        {
            OrderNumber = "PO-0002",
            Lines = [new PurchaseOrderLine { Id = 1, OrderedQuantity = 1, Description = "X" }],
        };

        var act = () => order.Receive("GRN-0001", null, [(1, 1)], Now, null, null);

        act.Should().Throw<DomainException>();
    }
}
