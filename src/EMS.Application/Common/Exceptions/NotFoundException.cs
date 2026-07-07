namespace EMS.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 404 by the API's exception middleware.</summary>
public sealed class NotFoundException(string entityName, object key)
    : Exception($"{entityName} with id '{key}' was not found.");
