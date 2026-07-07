using System.Reflection;

namespace EMS.Shared.Authorization;

/// <summary>
/// The full permission catalog (design §9.2, Appendix A). Permissions — not roles — are
/// the unit of enforcement; roles are named bundles of these strings stored as role
/// claims. Shared with the Blazor client so UI elements can hide what the API forbids.
/// </summary>
public static class Permissions
{
    /// <summary>JWT claim type carrying permission values (kept short — tokens stay compact).</summary>
    public const string ClaimType = "perm";

    public static class Users
    {
        public const string View = "users.view";
        public const string Manage = "users.manage";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Manage = "roles.manage";
    }

    public static class Employees
    {
        public const string View = "employees.view";
        public const string Create = "employees.create";
        public const string Edit = "employees.edit";
        public const string Delete = "employees.delete";
    }

    public static class Assets
    {
        public const string View = "assets.view";
        public const string Create = "assets.create";
        public const string Edit = "assets.edit";
        public const string Assign = "assets.assign";
        public const string Transfer = "assets.transfer";
        public const string Dispose = "assets.dispose";
    }

    public static class Inventory
    {
        public const string View = "inventory.view";
        public const string StockIn = "inventory.stockin";
        public const string StockOut = "inventory.stockout";
        public const string Adjust = "inventory.adjust";
    }

    public static class Fleet
    {
        public const string View = "fleet.view";
        public const string Manage = "fleet.manage";
        public const string LogFuel = "fleet.logfuel";
    }

    public static class Procurement
    {
        public const string View = "procurement.view";
        public const string Raise = "procurement.raise";
        public const string ApproveL1 = "procurement.approve.l1";
        public const string ApproveL2 = "procurement.approve.l2";
        public const string ManagePurchaseOrders = "procurement.po.manage";
        public const string ReceiveGoods = "procurement.grn.receive";
    }

    public static class Maintenance
    {
        public const string View = "maintenance.view";
        public const string Request = "maintenance.request";
        public const string Manage = "maintenance.manage";
        public const string Execute = "maintenance.execute";
    }

    public static class Leave
    {
        public const string View = "leave.view";
        public const string Request = "leave.request";
        public const string Approve = "leave.approve";
        public const string ManageTypes = "leave.types.manage";
    }

    public static class Reports
    {
        public const string View = "reports.view";
    }

    public static class Administration
    {
        public const string Settings = "admin.settings";
        public const string AuditLog = "admin.auditlog";
    }

    /// <summary>Every permission string in the catalog — used for Super Admin seeding.</summary>
    public static IReadOnlyList<string> All { get; } =
        typeof(Permissions)
            .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
}
