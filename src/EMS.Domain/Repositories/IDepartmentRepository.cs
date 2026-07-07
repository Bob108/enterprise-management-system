using EMS.Domain.Entities;

namespace EMS.Domain.Repositories;

public interface IDepartmentRepository
{
    Task<IReadOnlyList<(Department Department, int EmployeeCount)>> GetAllWithEmployeeCountAsync(
        CancellationToken cancellationToken = default);

    Task<Department?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> NameOrCodeTakenAsync(
        string name, string code, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> CountEmployeesAsync(int departmentId, CancellationToken cancellationToken = default);

    void Add(Department department);

    void Remove(Department department);
}
