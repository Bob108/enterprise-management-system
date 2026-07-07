using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Code).HasMaxLength(16).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(500);
        builder.Property(d => d.CreatedBy).HasMaxLength(256);
        builder.Property(d => d.ModifiedBy).HasMaxLength(256);

        // Unique among live rows only — a soft-deleted department frees its name/code.
        builder.HasIndex(d => d.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(d => d.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> builder)
    {
        builder.Property(d => d.Title).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(500);
        builder.Property(d => d.CreatedBy).HasMaxLength(256);
        builder.Property(d => d.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(d => d.Title).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.EmployeeNumber).HasMaxLength(16).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(32);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.ModifiedBy).HasMaxLength(256);

        // Employee numbers are never reused, even after soft delete — no filter.
        builder.HasIndex(e => e.EmployeeNumber).IsUnique();
        builder.HasIndex(e => e.Email).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.DepartmentId);
        builder.HasIndex(e => e.Status);

        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Designation)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DesignationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Relationship).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(32).IsRequired();

        builder.HasOne(c => c.Employee)
            .WithMany(e => e.EmergencyContacts)
            .HasForeignKey(c => c.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Matches the Employee soft-delete filter so contacts of hidden employees are hidden too.
        builder.HasQueryFilter(c => !c.Employee.IsDeleted);
    }
}
