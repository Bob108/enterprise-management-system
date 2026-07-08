namespace EMS.Domain.Common;

/// <summary>
/// A business-rule violation raised by entity behavior (e.g. an illegal asset status
/// transition). Mapped to HTTP 409 by the API's exception middleware.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
