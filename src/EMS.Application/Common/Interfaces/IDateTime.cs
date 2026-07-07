namespace EMS.Application.Common.Interfaces;

/// <summary>Clock abstraction so time-dependent rules are testable.</summary>
public interface IDateTime
{
    DateTime UtcNow { get; }
}
