namespace EMS.Shared.Enums;

/// <summary>PR lifecycle (design §6.6, Appendix B). Stored as tinyint.</summary>
public enum PurchaseRequestStatus : byte
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Converted = 5,
    Cancelled = 6,
}

public enum PurchaseOrderStatus : byte
{
    Draft = 1,
    Issued = 2,
    PartiallyReceived = 3,
    FullyReceived = 4,
    Closed = 5,
    Cancelled = 6,
}

/// <summary>
/// The design's G-6 boundary rule, applied per line: serialized items become Assets at
/// receiving; bulk consumables post into the inventory ledger.
/// </summary>
public enum ItemNature : byte
{
    Asset = 1,
    Consumable = 2,
}
