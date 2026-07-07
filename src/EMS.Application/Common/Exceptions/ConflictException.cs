namespace EMS.Application.Common.Exceptions;

/// <summary>Business-rule conflict (e.g. deleting a department that still has employees). Mapped to HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
