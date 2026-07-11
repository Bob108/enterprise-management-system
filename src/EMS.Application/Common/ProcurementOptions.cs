namespace EMS.Application.Common;

public sealed class ProcurementOptions
{
    public const string SectionName = "Procurement";

    /// <summary>
    /// Requests totalling this amount or more need a second approval by a Procurement
    /// Officer after the department manager's first approval (design §6.6).
    /// </summary>
    public decimal SecondApprovalThreshold { get; set; } = 100_000m;
}
