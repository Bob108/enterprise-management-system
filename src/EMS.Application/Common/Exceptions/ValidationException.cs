using FluentValidation.Results;

namespace EMS.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 400 with per-field errors by the API's exception middleware.</summary>
public sealed class ValidationException(IEnumerable<ValidationFailure> failures)
    : Exception("One or more validation failures occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = failures
        .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
        .ToDictionary(g => g.Key, g => g.ToArray());
}
