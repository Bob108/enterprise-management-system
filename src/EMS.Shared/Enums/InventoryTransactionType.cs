namespace EMS.Shared.Enums;

/// <summary>
/// Ledger movement types (design §6.4). MaintenanceIssue and GrnReceipt are reserved for
/// the Maintenance and Procurement modules, which post into the same ledger.
/// </summary>
public enum InventoryTransactionType : byte
{
    StockIn = 1,
    StockOut = 2,
    Adjustment = 3,
    MaintenanceIssue = 4,
    GrnReceipt = 5,
}
