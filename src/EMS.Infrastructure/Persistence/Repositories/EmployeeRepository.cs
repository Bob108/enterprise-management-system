using EMS.Domain.Entities;
using EMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence.Repositories;

public sealed class EmployeeRepository(EmsDbContext context) : IEmployeeRepository
{
    public async Task<(IReadOnlyList<Employee> Items, int TotalCount)> SearchAsync(
        EmployeeSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = context.Set<Employee>()
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.Designation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search;
            query = query.Where(e =>
                e.FirstName.Contains(term)
                || e.LastName.Contains(term)
                || e.Email.Contains(term)
                || e.EmployeeNumber.Contains(term));
        }

        if (criteria.DepartmentId is { } departmentId)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        if (criteria.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => context.Set<Employee>()
            .Include(e => e.Department)
            .Include(e => e.Designation)
            .Include(e => e.EmergencyContacts)
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> EmailTakenAsync(string email, int? excludeId, CancellationToken cancellationToken = default)
        => context.Set<Employee>().AnyAsync(
            e => e.Email == email && (excludeId == null || e.Id != excludeId), cancellationToken);

    public async Task<string> GetNextEmployeeNumberAsync(CancellationToken cancellationToken = default)
    {
        // Includes soft-deleted rows: numbers are never reused. The unique index is the
        // backstop for the (rare) concurrent-create race; configurable numbering formats
        // are a later Administration feature.
        var last = await context.Set<Employee>()
            .IgnoreQueryFilters()
            .OrderByDescending(e => e.Id)
            .Select(e => e.EmployeeNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (last is not null && last.StartsWith("EMP-") && int.TryParse(last[4..], out var n))
        {
            next = n + 1;
        }

        return $"EMP-{next:D4}";
    }

    public void Add(Employee employee) => context.Add(employee);

    public void Remove(Employee employee) => context.Remove(employee);

    public void SetOriginalRowVersion(Employee employee, byte[] rowVersion)
        => context.Entry(employee).Property(e => e.RowVersion).OriginalValue = rowVersion;
}
