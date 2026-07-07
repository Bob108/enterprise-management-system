using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence.Repositories;

public sealed class DesignationRepository(EmsDbContext context) : IDesignationRepository
{
    public async Task<IReadOnlyList<(Designation Designation, int EmployeeCount)>> GetAllWithEmployeeCountAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await context.Set<Designation>()
            .AsNoTracking()
            .OrderBy(d => d.Title)
            .Select(d => new { Designation = d, EmployeeCount = d.Employees.Count })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Designation, r.EmployeeCount)).ToList();
    }

    public Task<Designation?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Designation>().SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Designation>().AnyAsync(d => d.Id == id, cancellationToken);

    public Task<bool> TitleTakenAsync(string title, int? excludeId, CancellationToken cancellationToken = default)
        => context.Set<Designation>().AnyAsync(
            d => d.Title == title && (excludeId == null || d.Id != excludeId), cancellationToken);

    public Task<int> CountEmployeesAsync(int designationId, CancellationToken cancellationToken = default)
        => context.Set<Employee>().CountAsync(e => e.DesignationId == designationId, cancellationToken);

    public void Add(Designation designation) => context.Add(designation);

    public void Remove(Designation designation) => context.Remove(designation);
}
