using System.Security.Claims;
using EMS.Infrastructure.Identity;
using EMS.Shared.Authorization;
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
