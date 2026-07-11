using EMS.Domain.Common;
using EMS.Shared.Enums;

namespace EMS.Domain.Entities;

/// <summary>
/// Purchase request with a two-level approval workflow (design §6.6). All transitions are
/// entity behavior; segregation of duties (requester ≠ approver, L1 approver ≠ L2
/// approver, design §9.2) is enforced here, not in handlers.
/// </summary>
public class PurchaseRequest : BaseEntity, IAuditableEntity
{
    public string RequestNumber { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string? Justification { get; set; }
    public decimal TotalEstimatedCost { get; set; }

    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;
    public bool RequiresSecondApproval { get; set; }
    public int? FirstApprovedByUserId { get; set; }
    public string? FirstApprovedByName { get; set; }
    public DateTime? FirstApprovedAtUtc { get; set; }
    public string? SecondApprovedByName { get; set; }
    public DateTime? SecondApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public int? PurchaseOrderId { get; set; }

    public List<PurchaseRequestLine> Lines { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool AwaitingSecondApproval =>
        Status == PurchaseRequestStatus.Submitted && FirstApprovedAtUtc is not null;

    public void EnsureEditable()
    {
        if (Status != PurchaseRequestStatus.Draft)
        {
            throw new DomainException($"Request {RequestNumber} is {Status} and can no longer be edited.");
        }
    }

    public void RecalculateTotal()
        => TotalEstimatedCost = Lines.Sum(l => l.Quantity * l.EstimatedUnitCost);

    public void Submit(decimal secondApprovalThreshold)
    {
        EnsureEditable();
        if (Lines.Count == 0)
        {
            throw new DomainException("A request needs at least one line before it can be submitted.");
        }

        RecalculateTotal();
        RequiresSecondApproval = TotalEstimatedCost >= secondApprovalThreshold;
        Status = PurchaseRequestStatus.Submitted;
        RejectionReason = null;
    }

    /// <summary>First call records L1; when a second approval is required, a second call by a different approver completes it.</summary>
    public void Approve(int approverUserId, string approverName, DateTime nowUtc)
    {
        if (Status != PurchaseRequestStatus.Submitted)
        {
            throw new DomainException($"Request {RequestNumber} is {Status}; only submitted requests can be approved.");
        }

        if (approverUserId == RequestedByUserId)
        {
            throw new DomainException("You cannot approve your own purchase request.");
        }

        if (FirstApprovedAtUtc is null)
        {
            FirstApprovedByUserId = approverUserId;
            FirstApprovedByName = approverName;
            FirstApprovedAtUtc = nowUtc;
            if (!RequiresSecondApproval)
            {
                Status = PurchaseRequestStatus.Approved;
            }
        }
        else
        {
            if (approverUserId == FirstApprovedByUserId)
            {
                throw new DomainException("The second approval must come from a different approver.");
            }

            SecondApprovedByName = approverName;
            SecondApprovedAtUtc = nowUtc;
            Status = PurchaseRequestStatus.Approved;
        }
    }

    public void Reject(int approverUserId, string reason)
    {
        if (Status != PurchaseRequestStatus.Submitted)
        {
            throw new DomainException($"Request {RequestNumber} is {Status}; only submitted requests can be rejected.");
        }

        if (approverUserId == RequestedByUserId)
        {
            throw new DomainException("You cannot reject your own purchase request.");
        }

        Status = PurchaseRequestStatus.Rejected;
        RejectionReason = reason;
    }

    /// <summary>Sends a submitted request back to the requester for editing.</summary>
    public void ReturnToDraft()
    {
        if (Status != PurchaseRequestStatus.Submitted)
        {
            throw new DomainException($"Request {RequestNumber} is {Status}; only submitted requests can be returned.");
        }

        Status = PurchaseRequestStatus.Draft;
        ClearApprovals();
    }

    public void Cancel()
    {
        if (Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.Submitted))
        {
            throw new DomainException($"Request {RequestNumber} is {Status} and can no longer be cancelled.");
        }

        Status = PurchaseRequestStatus.Cancelled;
    }

    public void MarkConverted(int purchaseOrderId)
    {
        if (Status != PurchaseRequestStatus.Approved)
        {
            throw new DomainException($"Request {RequestNumber} is {Status}; only approved requests can be converted to an order.");
        }

        Status = PurchaseRequestStatus.Converted;
        PurchaseOrderId = purchaseOrderId;
    }

    private void ClearApprovals()
    {
        FirstApprovedByUserId = null;
        FirstApprovedByName = null;
        FirstApprovedAtUtc = null;
        SecondApprovedByName = null;
        SecondApprovedAtUtc = null;
    }
}

public class PurchaseRequestLine : BaseEntity
{
    public int PurchaseRequestId { get; set; }
    public PurchaseRequest PurchaseRequest { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public ItemNature Nature { get; set; }

    /// <summary>Required when Nature is Asset — receiving creates assets in this category.</summary>
    public int? AssetCategoryId { get; set; }
    public AssetCategory? AssetCategory { get; set; }

    /// <summary>Required when Nature is Consumable — receiving posts stock for this item.</summary>
    public int? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public int Quantity { get; set; }
    public decimal EstimatedUnitCost { get; set; }
}
