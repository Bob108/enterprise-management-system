using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

// Procurement documents are permanent history: they are never soft-deleted themselves,
// and repository queries use IgnoreQueryFilters so a soft-deleted master (department,
// supplier, category, item) never blanks out an old document. The corresponding EF
// "required navigation with query filter" model warning is suppressed in DI with this
// rationale.

public sealed class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        builder.Property(r => r.RequestNumber).HasMaxLength(16).IsRequired();
        builder.Property(r => r.RequestedByName).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Justification).HasMaxLength(1000);
        builder.Property(r => r.TotalEstimatedCost).HasPrecision(18, 2);
        builder.Property(r => r.FirstApprovedByName).HasMaxLength(256);
        builder.Property(r => r.SecondApprovedByName).HasMaxLength(256);
        builder.Property(r => r.RejectionReason).HasMaxLength(500);
        builder.Property(r => r.CreatedBy).HasMaxLength(256);
        builder.Property(r => r.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(r => r.RequestNumber).IsUnique();
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.RequestedByUserId);

        builder.HasOne(r => r.Department)
            .WithMany()
            .HasForeignKey(r => r.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseRequestLineConfiguration : IEntityTypeConfiguration<PurchaseRequestLine>
{
    public void Configure(EntityTypeBuilder<PurchaseRequestLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(300).IsRequired();
        builder.Property(l => l.EstimatedUnitCost).HasPrecision(18, 2);

        builder.HasOne(l => l.PurchaseRequest)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.AssetCategory)
            .WithMany()
            .HasForeignKey(l => l.AssetCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.InventoryItem)
            .WithMany()
            .HasForeignKey(l => l.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(16).IsRequired();
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.CreatedBy).HasMaxLength(256);
        builder.Property(o => o.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.Status);
        // One order per request (design §6.6).
        builder.HasIndex(o => o.PurchaseRequestId).IsUnique();

        builder.HasOne(o => o.PurchaseRequest)
            .WithMany()
            .HasForeignKey(o => o.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Supplier)
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.Property(l => l.Description).HasMaxLength(300).IsRequired();
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);

        builder.HasOne(l => l.PurchaseOrder)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.InventoryItem)
            .WithMany()
            .HasForeignKey(l => l.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GoodsReceivedNoteConfiguration : IEntityTypeConfiguration<GoodsReceivedNote>
{
    public void Configure(EntityTypeBuilder<GoodsReceivedNote> builder)
    {
        builder.Property(g => g.GrnNumber).HasMaxLength(16).IsRequired();
        builder.Property(g => g.ReceivedBy).HasMaxLength(256);
        builder.Property(g => g.Notes).HasMaxLength(500);

        builder.HasIndex(g => g.GrnNumber).IsUnique();

        builder.HasOne(g => g.PurchaseOrder)
            .WithMany(o => o.GoodsReceivedNotes)
            .HasForeignKey(g => g.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.Warehouse)
            .WithMany()
            .HasForeignKey(g => g.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GrnLineConfiguration : IEntityTypeConfiguration<GrnLine>
{
    public void Configure(EntityTypeBuilder<GrnLine> builder)
        => builder.HasOne(l => l.GoodsReceivedNote)
            .WithMany(g => g.Lines)
            .HasForeignKey(l => l.GoodsReceivedNoteId)
            .OnDelete(DeleteBehavior.Cascade);
}
