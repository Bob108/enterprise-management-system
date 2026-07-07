using System.Security.Claims;
using EMS.Domain.Entities;
using EMS.Infrastructure.Identity;
using EMS.Shared.Authorization;
using EMS.Shared.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EMS.Infrastructure.Persistence;

/// <summary>
/// Development/test bootstrap: applies migrations and seeds the role/permission baseline
/// (design Appendix A) plus demo accounts. Production applies migrations via the
/// pipeline's idempotent script instead (design §13) and never runs this.
/// </summary>
public sealed class EmsDbInitializer(
    EmsDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ILogger<EmsDbInitializer> logger)
{
    public const string SuperAdminRole = "Super Admin";

    /// <summary>Seed defaults from design Appendix A; editable at runtime by Super Admin.</summary>
    private static readonly Dictionary<string, string[]> RolePermissions = new()
    {
        [SuperAdminRole] = Permissions.All.ToArray(),
        ["HR"] =
        [
            Permissions.Users.View,
            Permissions.Employees.View, Permissions.Employees.Create,
            Permissions.Employees.Edit, Permissions.Employees.Delete,
            Permissions.Leave.View, Permissions.Leave.Approve, Permissions.Leave.ManageTypes,
            Permissions.Reports.View,
        ],
        ["Procurement Officer"] =
        [
            Permissions.Procurement.View, Permissions.Procurement.Raise,
            Permissions.Procurement.ApproveL2, Permissions.Procurement.ManagePurchaseOrders,
            Permissions.Inventory.View, Permissions.Assets.View, Permissions.Assets.Create,
            Permissions.Leave.Request, Permissions.Reports.View,
        ],
        ["Fleet Manager"] =
        [
            Permissions.Fleet.View, Permissions.Fleet.Manage, Permissions.Fleet.LogFuel,
            Permissions.Maintenance.View, Permissions.Maintenance.Request, Permissions.Maintenance.Manage,
            Permissions.Leave.Request, Permissions.Reports.View,
        ],
        ["Department Manager"] =
        [
            Permissions.Employees.View, Permissions.Assets.View,
            Permissions.Procurement.View, Permissions.Procurement.Raise, Permissions.Procurement.ApproveL1,
            Permissions.Leave.View, Permissions.Leave.Request, Permissions.Leave.Approve,
            Permissions.Maintenance.Request, Permissions.Reports.View,
        ],
        ["Maintenance Technician"] =
        [
            Permissions.Maintenance.View, Permissions.Maintenance.Execute,
            Permissions.Inventory.View, Permissions.Leave.Request,
        ],
        ["Store Keeper"] =
        [
            Permissions.Inventory.View, Permissions.Inventory.StockIn,
            Permissions.Inventory.StockOut, Permissions.Inventory.Adjust,
            Permissions.Procurement.ReceiveGoods, Permissions.Leave.Request,
        ],
        ["Employee"] =
        [
            Permissions.Procurement.Raise, Permissions.Leave.Request, Permissions.Maintenance.Request,
        ],
    };

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await context.Database.MigrateAsync(cancellationToken);
        await SeedRolesAsync();
        await SeedUsersAsync();
        await SeedHrAsync(cancellationToken);
        logger.LogInformation("Database migrated and seeded");
    }

    private async Task SeedRolesAsync()
    {
        foreach (var (roleName, permissions) in RolePermissions)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new ApplicationRole(roleName);
                var created = await roleManager.CreateAsync(role);
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{roleName}': {string.Join("; ", created.Errors.Select(e => e.Description))}");
                }
            }

            var existing = (await roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet();

            foreach (var permission in permissions.Where(p => !existing.Contains(p)))
            {
                await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
            }
        }
    }

    private async Task SeedUsersAsync()
    {
        // Dev-only demo accounts (design §16). Password policy: see DependencyInjection.
        await EnsureUserAsync("admin@ems.local", "Admin123!", "System Administrator", SuperAdminRole);
        await EnsureUserAsync("employee@ems.local", "Employee123!", "Demo Employee", "Employee");
    }

    private async Task SeedHrAsync(CancellationToken cancellationToken)
    {
        if (await context.Departments.AnyAsync(cancellationToken))
        {
            return;
        }

        var departments = new Dictionary<string, Department>
        {
            ["OPS"] = new() { Name = "Operations", Code = "OPS" },
            ["FIN"] = new() { Name = "Finance", Code = "FIN" },
            ["IT"] = new() { Name = "Information Technology", Code = "IT" },
            ["HR"] = new() { Name = "Human Resources", Code = "HR" },
            ["LOG"] = new() { Name = "Logistics", Code = "LOG" },
        };
        var designations = new Dictionary<string, Designation>
        {
            ["OM"] = new() { Title = "Operations Manager" },
            ["SE"] = new() { Title = "Software Engineer" },
            ["AC"] = new() { Title = "Accountant" },
            ["HO"] = new() { Title = "HR Officer" },
            ["LC"] = new() { Title = "Logistics Coordinator" },
            ["TE"] = new() { Title = "Technician" },
            ["DR"] = new() { Title = "Driver" },
            ["SK"] = new() { Title = "Storekeeper" },
        };
        context.Departments.AddRange(departments.Values);
        context.Designations.AddRange(designations.Values);

        (string First, string Last, string Dept, string Desig, EmploymentStatus Status, DateOnly Hire)[] people =
        [
            ("Amina", "Okoro", "OPS", "OM", EmploymentStatus.Active, new(2021, 3, 15)),
            ("Daniel", "Kimani", "IT", "SE", EmploymentStatus.Active, new(2022, 7, 1)),
            ("Grace", "Mwangi", "FIN", "AC", EmploymentStatus.Active, new(2020, 1, 20)),
            ("Peter", "Otieno", "LOG", "DR", EmploymentStatus.Active, new(2023, 2, 6)),
            ("Lucy", "Njeri", "HR", "HO", EmploymentStatus.Active, new(2019, 11, 4)),
            ("James", "Mutua", "OPS", "TE", EmploymentStatus.Probation, new(2025, 9, 22)),
            ("Sarah", "Wanjiku", "IT", "SE", EmploymentStatus.Active, new(2024, 5, 13)),
            ("David", "Omondi", "LOG", "SK", EmploymentStatus.Active, new(2021, 8, 30)),
            ("Esther", "Achieng", "FIN", "AC", EmploymentStatus.OnLeave, new(2022, 4, 11)),
            ("Michael", "Kariuki", "OPS", "TE", EmploymentStatus.Active, new(2023, 10, 2)),
            ("Ruth", "Wambui", "HR", "HO", EmploymentStatus.Active, new(2024, 1, 8)),
            ("Joseph", "Ndungu", "LOG", "DR", EmploymentStatus.Suspended, new(2020, 6, 17)),
            ("Mary", "Akinyi", "IT", "SE", EmploymentStatus.Active, new(2025, 3, 3)),
            ("Brian", "Kiprop", "LOG", "LC", EmploymentStatus.Active, new(2022, 12, 5)),
            ("Naomi", "Chebet", "OPS", "TE", EmploymentStatus.Active, new(2024, 8, 19)),
            ("Samuel", "Maina", "FIN", "AC", EmploymentStatus.Terminated, new(2018, 2, 26)),
        ];

        var number = 1;
        foreach (var p in people)
        {
            context.Employees.Add(new Employee
            {
                EmployeeNumber = $"EMP-{number++:D4}",
                FirstName = p.First,
                LastName = p.Last,
                Email = $"{p.First.ToLowerInvariant()}.{p.Last.ToLowerInvariant()}@northwind.example",
                Phone = $"+254 7{number:D2} 000 {number:D3}",
                Department = departments[p.Dept],
                Designation = designations[p.Desig],
                Status = p.Status,
                HireDate = p.Hire,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUserAsync(string email, string password, string displayName, string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create user '{email}': {string.Join("; ", created.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, role);
    }
}
