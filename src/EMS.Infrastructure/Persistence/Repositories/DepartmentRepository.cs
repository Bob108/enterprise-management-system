using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence.Repositories;

public sealed class DepartmentRepository(EmsDbContext context) : IDepartmentRepository
{
    public async Task<IReadOnlyList<(Department Department, int EmployeeCount)>> GetAllWithEmployeeCountAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Set<Department>()
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new { Department = d, EmployeeCount = d.Employees.Count })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Department, r.EmployeeCount)).ToList();
    }

    public Task<Department?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Department>().SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Department>().AnyAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<int, string>> GetNamesAsync(
        CancellationToken cancellationToken = default)
        => await context.Set<Department>()
            .IgnoreQueryFilters() // history may reference soft-deleted departments
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

    public Task<bool> NameOrCodeTakenAsync(
        string name, string code, int? excludeId, CancellationToken cancellationToken = default)
        => context.Set<Department>().AnyAsync(
            d => (d.Name == name || d.Code == code) && (excludeId == null || d.Id != excludeId),
            cancellationToken);

    public Task<int> CountEmployeesAsync(int departmentId, CancellationToken cancellationToken = default)
        => context.Set<Employee>().CountAsync(e => e.DepartmentId == departmentId, cancellationToken);

    public void Add(Department department) => context.Add(department);

    public void Remove(Department department) => context.Remove(department);
}
