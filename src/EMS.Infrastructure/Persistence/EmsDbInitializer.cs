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
        await SeedAssetsAsync(cancellationToken);
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

    private async Task SeedAssetsAsync(CancellationToken cancellationToken)
    {
        if (await context.AssetCategories.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = new Dictionary<string, AssetCategory>
        {
            // Method, useful life, residual per design §6.3 examples.
            ["ITE"] = new() { Name = "IT Equipment", CodePrefix = "ITE", Method = DepreciationMethod.StraightLine, UsefulLifeMonths = 36, ResidualRate = 0.10m },
            ["FUR"] = new() { Name = "Furniture", CodePrefix = "FUR", Method = DepreciationMethod.StraightLine, UsefulLifeMonths = 60, ResidualRate = 0.05m },
            ["MCH"] = new() { Name = "Machinery", CodePrefix = "MCH", Method = DepreciationMethod.DecliningBalance, UsefulLifeMonths = 84, ResidualRate = 0.10m },
            ["OFC"] = new() { Name = "Office Equipment", CodePrefix = "OFC", Method = DepreciationMethod.StraightLine, UsefulLifeMonths = 48, ResidualRate = 0.05m },
            ["COM"] = new() { Name = "Communication Devices", CodePrefix = "COM", Method = DepreciationMethod.StraightLine, UsefulLifeMonths = 24, ResidualRate = 0.10m },
        };
        var suppliers = new Dictionary<string, Supplier>
        {
            ["TS"] = new() { Name = "TechSource Ltd", ContactPerson = "Alice Kamau", Email = "sales@techsource.example", Phone = "+254 720 100 200" },
            ["OF"] = new() { Name = "OfficeFit Interiors", ContactPerson = "Brian Odhiambo", Email = "info@officefit.example" },
            ["MW"] = new() { Name = "MachineWorks EA", ContactPerson = "Carol Njoroge", Email = "orders@machineworks.example" },
            ["GC"] = new() { Name = "GlobalComm Distributors", Email = "support@globalcomm.example" },
        };
        context.AssetCategories.AddRange(categories.Values);
        context.Suppliers.AddRange(suppliers.Values);

        var departments = await context.Departments.ToDictionaryAsync(d => d.Code, cancellationToken);
        var employees = await context.Employees.OrderBy(e => e.Id).ToListAsync(cancellationToken);

        (string Name, string Cat, string Sup, string Dept, decimal Cost, DateOnly Purchased, string? Serial)[] items =
        [
            ("Dell Latitude 5540 Laptop", "ITE", "TS", "IT", 145_000m, new(2024, 2, 10), "DL5540-8821"),
            ("Dell Latitude 5540 Laptop", "ITE", "TS", "FIN", 145_000m, new(2024, 2, 10), "DL5540-8822"),
            ("HP ProBook 450 Laptop", "ITE", "TS", "OPS", 118_000m, new(2024, 6, 5), "HPPB-3310"),
            ("HP ProBook 450 Laptop", "ITE", "TS", "HR", 118_000m, new(2024, 6, 5), "HPPB-3311"),
            ("MacBook Pro 14 M3", "ITE", "TS", "IT", 320_000m, new(2025, 1, 20), "MBP14-0042"),
            ("Dell PowerEdge T360 Server", "ITE", "TS", "IT", 480_000m, new(2024, 9, 1), "PET360-001"),
            ("Lenovo ThinkCentre Desktop", "ITE", "TS", "FIN", 85_000m, new(2023, 11, 12), "LTC-7701"),
            ("Lenovo ThinkCentre Desktop", "ITE", "TS", "OPS", 85_000m, new(2023, 11, 12), "LTC-7702"),
            ("Executive Desk (Mahogany)", "FUR", "OF", "HR", 65_000m, new(2023, 8, 15), null),
            ("Ergonomic Chair Herman Miller", "FUR", "OF", "IT", 92_000m, new(2024, 3, 22), null),
            ("Conference Table 12-seat", "FUR", "OF", "OPS", 150_000m, new(2023, 5, 30), null),
            ("Reception Sofa Set", "FUR", "OF", "HR", 78_000m, new(2024, 10, 8), null),
            ("Forklift 2.5T Toyota", "MCH", "MW", "LOG", 2_800_000m, new(2023, 4, 18), "FLT-25T-889"),
            ("Pallet Wrapping Machine", "MCH", "MW", "LOG", 950_000m, new(2024, 7, 25), "PWM-2024-11"),
            ("Diesel Generator 60kVA", "MCH", "MW", "OPS", 1_450_000m, new(2023, 12, 1), "DG60-5501"),
            ("Canon imageRUNNER Printer", "OFC", "TS", "FIN", 210_000m, new(2024, 4, 14), "CIR-2630-77"),
            ("Epson Projector EB-2250U", "OFC", "TS", "OPS", 130_000m, new(2024, 8, 19), "EPJ-2250-15"),
            ("Paper Shredder Fellowes", "OFC", "OF", "FIN", 38_000m, new(2025, 2, 3), null),
            ("Air Conditioner 24000BTU", "OFC", "OF", "IT", 95_000m, new(2023, 10, 27), "AC24-9912"),
            ("iPhone 15 Pro", "COM", "GC", "OPS", 165_000m, new(2025, 3, 10), "IP15P-4410"),
            ("iPhone 15 Pro", "COM", "GC", "IT", 165_000m, new(2025, 3, 10), "IP15P-4411"),
            ("Samsung Galaxy S24", "COM", "GC", "HR", 125_000m, new(2024, 12, 2), "SGS24-0087"),
            ("Motorola Two-Way Radio Set", "COM", "GC", "LOG", 88_000m, new(2024, 5, 16), "MTR-SET-22"),
            ("Starlink Business Kit", "COM", "GC", "IT", 280_000m, new(2025, 6, 1), "SLB-2025-03"),
        ];

        var counters = new Dictionary<string, int>();
        var assets = new List<Asset>();
        foreach (var item in items)
        {
            counters[item.Cat] = counters.GetValueOrDefault(item.Cat) + 1;
            assets.Add(new Asset
            {
                AssetCode = $"{item.Cat}-{counters[item.Cat]:D4}",
                Name = item.Name,
                Category = categories[item.Cat],
                Supplier = suppliers[item.Sup],
                DepartmentId = departments[item.Dept].Id,
                PurchaseDate = item.Purchased,
                PurchaseCost = item.Cost,
                SerialNumber = item.Serial,
                WarrantyExpiryDate = item.Purchased.AddYears(item.Cat is "ITE" or "COM" ? 2 : 1),
            });
        }
        context.Assets.AddRange(assets);

        // Lifecycle variety so lists, histories and reports demo well.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (employees.Count >= 11)
        {
            assets[0].AssignTo(employees[1].Id, "New in box", today.AddMonths(-10));
            assets[2].AssignTo(employees[0].Id, null, today.AddMonths(-7));
            assets[4].AssignTo(employees[6].Id, "Includes charger and sleeve", today.AddMonths(-3));
            assets[19].AssignTo(employees[3].Id, null, today.AddMonths(-2));
            assets[21].AssignTo(employees[10].Id, null, today.AddMonths(-1));

            // One returned assignment in history.
            assets[7].AssignTo(employees[8].Id, null, today.AddMonths(-12));
            assets[7].Return("Returned on role change", today.AddMonths(-4));
        }

        assets[16].MarkUnderRepair();
        assets[22].ReportLost(today.AddMonths(-1));
        assets[6].TransferTo(departments["IT"].Id, "Reallocated after finance refresh", today.AddMonths(-5));

        await context.SaveChangesAsync(cancellationToken);

        // Two retired assets with disposal records (book value ≈ cost here; the catch-up
        // job posts historical depreciation on first run after seeding).
        assets[18].Dispose(DisposalMethod.Sold, 40_000m, assets[18].PurchaseCost, "Replaced by split units", today.AddMonths(-2));
        assets[11].Dispose(DisposalMethod.Donated, null, assets[11].PurchaseCost, "Donated to community center", today.AddMonths(-1));
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
