using System.Reflection;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence;

public class EmsDbContext(DbContextOptions<EmsDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>(options), IUnitOfWork
{
    private static readonly MethodInfo ApplySoftDeleteFilterMethod = typeof(EmsDbContext)
        .GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmsDbContext).Assembly);

        // Global query filter: soft-deleted rows are invisible unless IgnoreQueryFilters()
        // is used explicitly (design §7.2).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                ApplySoftDeleteFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, [modelBuilder]);
            }
        }
    }

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
        => modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
}
