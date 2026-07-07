namespace EMS.Shared.Enums;

/// <summary>Employment lifecycle states (design Appendix B). Stored as tinyint.</summary>
public enum EmploymentStatus : byte
{
    Active = 1,
    Probation = 2,
    OnLeave = 3,
    Suspended = 4,
    Terminated = 5,
}
