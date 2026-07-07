using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.EntityType).HasMaxLength(128);
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.Action).HasMaxLength(16);
        builder.Property(a => a.UserName).HasMaxLength(256);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.TimestampUtc);
    }
}
