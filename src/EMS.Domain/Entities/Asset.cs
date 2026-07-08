using EMS.Domain.Common;
using EMS.Shared.Enums;

namespace EMS.Domain.Entities;

/// <summary>
/// An individually tracked item (design §6.2). The status machine is enforced here — a
/// handler (or a stale client) cannot force an illegal transition; violations raise
/// <see cref="DomainException"/> which the API maps to 409.
/// </summary>
public class Asset : BaseEntity, IAuditableEntity, ISoftDeletable
{
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public AssetCategory Category { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string? SerialNumber { get; set; }
    public string? Model { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public decimal PurchaseCost { get; set; }
    public DateOnly? WarrantyExpiryDate { get; set; }
    public string? Notes { get; set; }

    public AssetStatus Status { get; set; } = AssetStatus.Available;
    public int? CurrentAssigneeEmployeeId { get; set; }
    public Employee? CurrentAssignee { get; set; }

    public List<AssetAssignment> Assignments { get; set; } = [];
    public List<AssetTransfer> Transfers { get; set; } = [];
    public AssetDisposal? Disposal { get; set; }
    public List<DepreciationEntry> DepreciationEntries { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }

    public AssetAssignment AssignTo(int employeeId, string? conditionNotes, DateOnly date)
    {
        if (Status != AssetStatus.Available)
        {
            throw new DomainException($"Only available assets can be assigned; '{AssetCode}' is {Status}.");
        }

        Status = AssetStatus.Assigned;
        CurrentAssigneeEmployeeId = employeeId;

        var assignment = new AssetAssignment
        {
            Asset = this,
            EmployeeId = employeeId,
            AssignedOn = date,
            ConditionOut = conditionNotes,
        };
        Assignments.Add(assignment);
        return assignment;
    }

    public void Return(string? conditionNotes, DateOnly date)
    {
        if (Status != AssetStatus.Assigned)
        {
            throw new DomainException($"'{AssetCode}' is not assigned; nothing to return.");
        }

        CloseOpenAssignment(conditionNotes, date);
        Status = AssetStatus.Available;
        CurrentAssigneeEmployeeId = null;
    }

    public void MarkUnderRepair()
    {
        if (Status is not (AssetStatus.Available or AssetStatus.Assigned))
        {
            throw new DomainException($"'{AssetCode}' cannot go to repair from {Status}.");
        }

        Status = AssetStatus.UnderRepair;
    }

    /// <summary>Returns to the assignee if one is on record, otherwise to Available.</summary>
    public void MarkRepaired()
    {
        if (Status != AssetStatus.UnderRepair)
        {
            throw new DomainException($"'{AssetCode}' is not under repair.");
        }

        Status = CurrentAssigneeEmployeeId is null ? AssetStatus.Available : AssetStatus.Assigned;
    }

    /// <summary>Closes any open assignment — accountability transfers to the loss record.</summary>
    public void ReportLost(DateOnly date)
    {
        if (Status is not (AssetStatus.Available or AssetStatus.Assigned))
        {
            throw new DomainException($"'{AssetCode}' cannot be reported lost from {Status}.");
        }

        if (Status == AssetStatus.Assigned)
        {
            CloseOpenAssignment("Reported lost", date);
        }

        Status = AssetStatus.Lost;
        CurrentAssigneeEmployeeId = null;
    }

    public void Recover()
    {
        if (Status != AssetStatus.Lost)
        {
            throw new DomainException($"'{AssetCode}' is not lost.");
        }

        Status = AssetStatus.Available;
    }

    public void TransferTo(int toDepartmentId, string? reason, DateOnly date)
    {
        if (Status is AssetStatus.Retired or AssetStatus.Lost)
        {
            throw new DomainException($"'{AssetCode}' cannot be transferred while {Status}.");
        }

        if (toDepartmentId == DepartmentId)
        {
            throw new DomainException("Target department is the same as the current department.");
        }

        Transfers.Add(new AssetTransfer
        {
            Asset = this,
            FromDepartmentId = DepartmentId,
            ToDepartmentId = toDepartmentId,
            TransferredOn = date,
            Reason = reason,
        });
        DepartmentId = toDepartmentId;
    }

    /// <summary>Terminal. Only idle (Available) or Lost (write-off) assets can be retired — assigned or in-repair assets must be resolved first.</summary>
    public AssetDisposal Dispose(DisposalMethod method, decimal? proceeds, decimal bookValue, string? reason, DateOnly date)
    {
        if (Status is not (AssetStatus.Available or AssetStatus.Lost))
        {
            throw new DomainException($"'{AssetCode}' must be Available or Lost to retire; it is {Status}.");
        }

        Status = AssetStatus.Retired;
        Disposal = new AssetDisposal
        {
            Asset = this,
            DisposedOn = date,
            Method = method,
            Proceeds = proceeds,
            GainLoss = (proceeds ?? 0m) - bookValue,
            Reason = reason,
        };
        return Disposal;
    }

    private void CloseOpenAssignment(string? conditionNotes, DateOnly date)
    {
        var open = Assignments.SingleOrDefault(a => a.ReturnedOn is null)
            ?? throw new DomainException($"No open assignment found for '{AssetCode}' (assignment history not loaded?).");
        open.ReturnedOn = date;
        open.ConditionIn = conditionNotes;
    }
}
