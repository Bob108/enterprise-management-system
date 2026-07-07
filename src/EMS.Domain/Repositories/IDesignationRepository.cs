using EMS.Domain.Entities;

namespace EMS.Domain.Repositories;

public interface IDesignationRepository
{
    Task<IReadOnlyList<(Designation Designation, int EmployeeCount)>> GetAllWithEmployeeCountAsync(
        CancellationToken cancellationToken = default);

    Task<Designation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> TitleTakenAsync(string title, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> CountEmployeesAsync(int designationId, CancellationToken cancellationToken = default);

    void Add(Designation designation);

    void Remove(Designation designation);
}
