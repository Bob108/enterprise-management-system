using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public sealed class AssetCategoryConfiguration : IEntityTypeConfiguration<AssetCategory>
{
    public void Configure(EntityTypeBuilder<AssetCategory> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.CodePrefix).HasMaxLength(6).IsRequired();
        builder.Property(c => c.ResidualRate).HasPrecision(5, 4);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.CreatedBy).HasMaxLength(256);
        builder.Property(c => c.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(c => c.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => c.CodePrefix).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ContactPerson).HasMaxLength(200);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.Phone).HasMaxLength(32);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.CreatedBy).HasMaxLength(256);
        builder.Property(s => s.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(s => s.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.Property(a => a.AssetCode).HasMaxLength(16).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.SerialNumber).HasMaxLength(100);
        builder.Property(a => a.Model).HasMaxLength(100);
        builder.Property(a => a.PurchaseCost).HasPrecision(18, 2);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.CreatedBy).HasMaxLength(256);
        builder.Property(a => a.ModifiedBy).HasMaxLength(256);

        // Asset codes are never reused, even after soft delete — no filter.
        builder.HasIndex(a => a.AssetCode).IsUnique();
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.DepartmentId);
        builder.HasIndex(a => a.CategoryId);

        builder.HasOne(a => a.Category)
            .WithMany(c => c.Assets)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Department)
            .WithMany()
            .HasForeignKey(a => a.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Supplier)
            .WithMany(s => s.Assets)
            .HasForeignKey(a => a.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CurrentAssignee)
            .WithMany()
            .HasForeignKey(a => a.CurrentAssigneeEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        builder.Property(a => a.ConditionOut).HasMaxLength(500);
        builder.Property(a => a.ConditionIn).HasMaxLength(500);

        builder.HasIndex(a => a.AssetId);
        builder.HasIndex(a => a.EmployeeId);

        builder.HasOne(a => a.Asset)
            .WithMany(x => x.Assignments)
            .HasForeignKey(a => a.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // History follows its filtered principals (same pattern as EmergencyContact).
        builder.HasQueryFilter(a => !a.Asset.IsDeleted && !a.Employee.IsDeleted);
    }
}

public sealed class AssetTransferConfiguration : IEntityTypeConfiguration<AssetTransfer>
{
    public void Configure(EntityTypeBuilder<AssetTransfer> builder)
    {
        builder.Property(t => t.Reason).HasMaxLength(500);
        builder.HasIndex(t => t.AssetId);

        builder.HasOne(t => t.Asset)
            .WithMany(a => a.Transfers)
            .HasForeignKey(t => t.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.Asset.IsDeleted);
    }
}

public sealed class AssetDisposalConfiguration : IEntityTypeConfiguration<AssetDisposal>
{
    public void Configure(EntityTypeBuilder<AssetDisposal> builder)
    {
        builder.Property(d => d.Proceeds).HasPrecision(18, 2);
        builder.Property(d => d.GainLoss).HasPrecision(18, 2);
        builder.Property(d => d.Reason).HasMaxLength(500);

        builder.HasIndex(d => d.AssetId).IsUnique();

        builder.HasOne(d => d.Asset)
            .WithOne(a => a.Disposal)
            .HasForeignKey<AssetDisposal>(d => d.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(d => !d.Asset.IsDeleted);
    }
}

public sealed class DepreciationEntryConfiguration : IEntityTypeConfiguration<DepreciationEntry>
{
    public void Configure(EntityTypeBuilder<DepreciationEntry> builder)
    {
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.BookValueAfter).HasPrecision(18, 2);

        // One posting per asset per month — the idempotency backstop.
        builder.HasIndex(e => new { e.AssetId, e.Year, e.Month }).IsUnique();

        builder.HasOne(e => e.Asset)
            .WithMany(a => a.DepreciationEntries)
            .HasForeignKey(e => e.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => !e.Asset.IsDeleted);
    }
}
