using System.Text.Json;
using EMS.Application.Common.Interfaces;
using EMS.Domain.Common;
using EMS.Domain.Entities;
using EMS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EMS.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Cross-cutting persistence behavior (design §7.2, FR-14):
/// 1. converts hard deletes of <see cref="ISoftDeletable"/> entities into soft deletes;
/// 2. stamps <see cref="IAuditableEntity"/> columns from the current user and clock;
/// 3. writes an <see cref="AuditLog"/> row per change with a before/after JSON snapshot.
/// Audit rows are saved immediately after the business save so that identity-generated
/// keys are known; a crash between the two saves loses only the audit rows, never the
/// business change (accepted trade-off, revisit if compliance demands strict atomicity).
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, IDateTime clock)
    : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>Auth plumbing and the audit table itself are not audited.</summary>
    private static readonly Type[] ExcludedEntityTypes = [typeof(AuditLog), typeof(RefreshToken)];

    /// <summary>Never captured in snapshots.</summary>
    private static readonly string[] ExcludedProperties =
        ["PasswordHash", "SecurityStamp", "ConcurrencyStamp", "RowVersion", "TokenHash"];

    private readonly List<PendingAudit> _pending = [];
    private bool _isSavingAuditLogs;

    private sealed record PendingAudit(EntityEntry Entry, string Action, string? Changes);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        PrepareAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        PrepareAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context && FlushPending(context) is { Count: > 0 })
        {
            context.SaveChanges();
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && FlushPending(context) is { Count: > 0 })
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void PrepareAudit(DbContext? context)
    {
        if (context is null || _isSavingAuditLogs)
        {
            return;
        }

        var now = clock.UtcNow;
        var userName = currentUser.UserName;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || ExcludedEntityTypes.Contains(entry.Entity.GetType()))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Deleted => "Deleted",
                _ => "Modified",
            };

            var changes = BuildChangesJson(entry);

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
            }

            if (entry.Entity is IAuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedBy = userName;
                }
                else
                {
                    auditable.ModifiedAtUtc = now;
                    auditable.ModifiedBy = userName;
                }
            }

            _pending.Add(new PendingAudit(entry, action, changes));
        }
    }

    private List<AuditLog> FlushPending(DbContext context)
    {
        if (_isSavingAuditLogs || _pending.Count == 0)
        {
            return [];
        }

        var now = clock.UtcNow;
        var logs = _pending
            .Select(p => new AuditLog
            {
                UserId = currentUser.UserId,
                UserName = currentUser.UserName,
                EntityType = p.Entry.Metadata.ClrType.Name,
                EntityId = GetKeyString(p.Entry),
                Action = p.Action,
                Changes = p.Changes,
                TimestampUtc = now,
            })
            .ToList();

        _pending.Clear();

        _isSavingAuditLogs = true;
        try
        {
            context.Set<AuditLog>().AddRange(logs);
        }
        finally
        {
            _isSavingAuditLogs = false;
        }

        return logs;
    }

    private static string GetKeyString(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return string.Empty;
        }

        return string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue));
    }

    private static string? BuildChangesJson(EntityEntry entry)
    {
        var changes = new Dictionary<string, Dictionary<string, object?>>();

        foreach (var property in entry.Properties)
        {
            if (ExcludedProperties.Contains(property.Metadata.Name))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[property.Metadata.Name] = new() { ["new"] = property.CurrentValue };
                    break;
                case EntityState.Deleted:
                    changes[property.Metadata.Name] = new() { ["old"] = property.OriginalValue };
                    break;
                case EntityState.Modified when property.IsModified
                    && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[property.Metadata.Name] = new()
                    {
                        ["old"] = property.OriginalValue,
                        ["new"] = property.CurrentValue,
                    };
                    break;
            }
        }

        return changes.Count == 0 ? null : JsonSerializer.Serialize(changes, JsonOptions);
    }
}
