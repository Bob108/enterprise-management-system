namespace EMS.Domain.Common;

/// <summary>
/// Commits a use case's changes atomically. Implemented by the EF DbContext — it already
/// is a unit of work; this interface just keeps Application free of EF (design §5.4).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
