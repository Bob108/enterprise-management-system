namespace EMS.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 403 by the API's exception middleware.</summary>
public sealed class ForbiddenAccessException(string permission)
    : Exception($"Missing required permission '{permission}'.")
{
    public string Permission { get; } = permission;
}
