using EMS.Domain.Entities;
using EMS.Shared.Enums;

namespace EMS.Domain.Repositories;

public sealed record EmployeeSearchCriteria(
    string? Search,
    int? DepartmentId,
    EmploymentStatus? Status,
    int Page,
    int PageSize);

public interface IEmployeeRepository
{
    Task<(IReadOnlyList<Employee> Items, int TotalCount)> SearchAsync(
        EmployeeSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Tracked, with department/designation/contacts loaded.</summary>
    Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> EmailTakenAsync(string email, int? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Next number in the EMP-#### sequence (unique index is the race backstop).</summary>
    Task<string> GetNextEmployeeNumberAsync(CancellationToken cancellationToken = default);

    void Add(Employee employee);

    void Remove(Employee employee);

    /// <summary>Arms optimistic concurrency: the update fails with a conflict if the row changed since the client read it.</summary>
    void SetOriginalRowVersion(Employee employee, byte[] rowVersion);
}
