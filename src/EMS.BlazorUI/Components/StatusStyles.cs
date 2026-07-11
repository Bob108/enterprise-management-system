using EMS.Shared.Enums;
using MudBlazor;

namespace EMS.BlazorUI.Components;

public static class StatusStyles
{
    public static Color For(AssetStatus status) => status switch
    {
        AssetStatus.Available => Color.Success,
        AssetStatus.Assigned => Color.Info,
        AssetStatus.UnderRepair => Color.Warning,
        AssetStatus.Lost => Color.Error,
        AssetStatus.Retired => Color.Dark,
        _ => Color.Default,
    };

    public static Color For(PurchaseRequestStatus status) => status switch
    {
        PurchaseRequestStatus.Draft => Color.Default,
        PurchaseRequestStatus.Submitted => Color.Info,
        PurchaseRequestStatus.Approved => Color.Success,
        PurchaseRequestStatus.Rejected => Color.Error,
        PurchaseRequestStatus.Converted => Color.Primary,
        PurchaseRequestStatus.Cancelled => Color.Dark,
        _ => Color.Default,
    };

    public static Color For(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => Color.Default,
        PurchaseOrderStatus.Issued => Color.Info,
        PurchaseOrderStatus.PartiallyReceived => Color.Warning,
        PurchaseOrderStatus.FullyReceived => Color.Success,
        PurchaseOrderStatus.Closed => Color.Dark,
        PurchaseOrderStatus.Cancelled => Color.Error,
        _ => Color.Default,
    };

    public static Color For(EmploymentStatus status) => status switch
    {
        EmploymentStatus.Active => Color.Success,
        EmploymentStatus.Probation => Color.Info,
        EmploymentStatus.OnLeave => Color.Warning,
        EmploymentStatus.Suspended => Color.Error,
        EmploymentStatus.Terminated => Color.Dark,
        _ => Color.Default,
    };
}
