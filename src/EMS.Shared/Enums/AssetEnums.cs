namespace EMS.Shared.Enums;

/// <summary>Asset lifecycle states (design §6.2, Appendix B). Stored as tinyint.</summary>
public enum AssetStatus : byte
{
    Available = 1,
    Assigned = 2,
    UnderRepair = 3,
    Lost = 4,
    Retired = 5,
}

public enum DepreciationMethod : byte
{
    StraightLine = 1,
    DecliningBalance = 2,
}

public enum DisposalMethod : byte
{
    Sold = 1,
    Scrapped = 2,
    Donated = 3,
    WrittenOff = 4,
}
