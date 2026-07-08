using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Code).HasMaxLength(16).IsRequired();
        builder.Property(w => w.Location).HasMaxLength(200);
        builder.Property(w => w.CreatedBy).HasMaxLength(256);
        builder.Property(w => w.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(w => w.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(w => w.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.Property(i => i.ItemCode).HasMaxLength(16).IsRequired();
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Category).HasMaxLength(100);
        builder.Property(i => i.Unit).HasMaxLength(16).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.CreatedBy).HasMaxLength(256);
        builder.Property(i => i.ModifiedBy).HasMaxLength(256);

        // Item codes are never reused, even after soft delete — no filter.
        builder.HasIndex(i => i.ItemCode).IsUnique();
        builder.HasIndex(i => i.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> builder)
    {
        // One balance row per item per warehouse.
        builder.HasIndex(s => new { s.ItemId, s.WarehouseId }).IsUnique();

        builder.HasOne(s => s.Item)
            .WithMany(i => i.StockLevels)
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Warehouse)
            .WithMany(w => w.StockLevels)
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(s => !s.Item.IsDeleted && !s.Warehouse.IsDeleted);

        // Defense in depth behind the conditional UPDATE (design §7.3).
        builder.ToTable(t => t.HasCheckConstraint("CK_StockLevels_Quantity", "[Quantity] >= 0"));
    }
}

public sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.Property(t => t.Reason).HasMaxLength(500);
        builder.Property(t => t.Reference).HasMaxLength(100);
        builder.Property(t => t.PerformedBy).HasMaxLength(256);

        builder.HasIndex(t => new { t.ItemId, t.Id });
        builder.HasIndex(t => t.PerformedAtUtc);

        builder.HasOne(t => t.Item)
            .WithMany()
            .HasForeignKey(t => t.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Warehouse)
            .WithMany()
            .HasForeignKey(t => t.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(t => !t.Item.IsDeleted && !t.Warehouse.IsDeleted);
    }
}
