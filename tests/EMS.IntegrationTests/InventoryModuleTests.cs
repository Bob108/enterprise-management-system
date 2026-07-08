using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EMS.Shared.Auth;
using EMS.Shared.Common;
using EMS.Shared.Enums;
using EMS.Shared.Inventory;
using FluentAssertions;

namespace EMS.IntegrationTests;

public class InventoryModuleTests(EmsApiFactory factory) : IClassFixture<EmsApiFactory>
{
    [Fact]
    public async Task Items_list_returns_seeded_items_with_totals_and_low_stock_flags()
    {
        var client = await LoginAdminAsync();

        var page = await client.GetFromJsonAsync<PagedResult<InventoryItemListDto>>(
            "/api/v1/inventory/items?page=1&pageSize=50");

        page!.TotalCount.Should().BeGreaterThanOrEqualTo(10);
        page.Items.Should().Contain(i => i.IsBelowMinimum);       // seeded toner is under minimum
        page.Items.Should().Contain(i => i.TotalOnHand > 0);
    }

    [Fact]
    public async Task Low_stock_filter_returns_only_flagged_items()
    {
        var client = await LoginAdminAsync();

        var page = await client.GetFromJsonAsync<PagedResult<InventoryItemListDto>>(
            "/api/v1/inventory/items?lowStockOnly=true&page=1&pageSize=50");

        page!.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(i => i.IsBelowMinimum);
    }

    [Fact]
    public async Task Stock_in_then_out_updates_balance_and_writes_signed_ledger_rows()
    {
        var client = await LoginAdminAsync();
        var warehouseId = await GetWarehouseIdAsync(client);
        var itemId = await CreateItemAsync(client, $"Ledger Test {Guid.NewGuid():N}"[..30]);

        (await client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/stock-in",
            new StockMovementRequest(warehouseId, 10, "opening", "PO-1")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/stock-out",
            new StockMovementRequest(warehouseId, 4, "issued to IT", null)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await client.GetFromJsonAsync<InventoryItemDetailDto>(
            $"/api/v1/inventory/items/{itemId}");
        detail!.StockLevels.Single(s => s.WarehouseId == warehouseId).Quantity.Should().Be(6);

        var ledger = await client.GetFromJsonAsync<List<InventoryTransactionDto>>(
            $"/api/v1/inventory/items/{itemId}/transactions");
        ledger!.Should().HaveCount(2);
        ledger.Sum(t => t.QuantityChange).Should().Be(6); // ledger sum equals the balance
        ledger.Should().ContainSingle(t => t.Type == InventoryTransactionType.StockOut && t.QuantityChange == -4);
    }

    [Fact]
    public async Task Stock_out_beyond_available_returns_409_and_leaves_balance_intact()
    {
        var client = await LoginAdminAsync();
        var warehouseId = await GetWarehouseIdAsync(client);
        var itemId = await CreateItemAsync(client, $"Overdraw Test {Guid.NewGuid():N}"[..30]);

        await client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/stock-in",
            new StockMovementRequest(warehouseId, 3, null, null));

        var response = await client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/stock-out",
            new StockMovementRequest(warehouseId, 5, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var detail = await client.GetFromJsonAsync<InventoryItemDetailDto>(
            $"/api/v1/inventory/items/{itemId}");
        detail!.StockLevels.Single(s => s.WarehouseId == warehouseId).Quantity.Should().Be(3);

        // The refused movement must not appear in the ledger either.
        var ledger = await client.GetFromJsonAsync<List<InventoryTransactionDto>>(
            $"/api/v1/inventory/items/{itemId}/transactions");
        ledger!.Should().ContainSingle();
    }

    [Fact]
    public async Task Concurrent_stock_outs_of_the_last_unit_cannot_both_succeed()
    {
        var client = await LoginAdminAsync();
        var warehouseId = await GetWarehouseIdAsync(client);
        var itemId = await CreateItemAsync(client, $"Race Test {Guid.NewGuid():N}"[..30]);

        await client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/stock-in",
            new StockMovementRequest(warehouseId, 1, null, null));

        // Two simultaneous withdrawals of the single remaining unit: the conditional
        // UPDATE (design §7.3) guarantees exactly one wins.
        var tasks = Enumerable.Range(0, 2)
            .Select(_ => client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/stock-out",
                new StockMovementRequest(warehouseId, 1, "race", null)))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        var detail = await client.GetFromJsonAsync<InventoryItemDetailDto>(
            $"/api/v1/inventory/items/{itemId}");
        detail!.StockLevels.Single(s => s.WarehouseId == warehouseId).Quantity.Should().Be(0);
    }

    [Fact]
    public async Task Employee_role_cannot_stock_in()
    {
        var admin = await LoginAdminAsync();
        var warehouseId = await GetWarehouseIdAsync(admin);
        var itemId = await CreateItemAsync(admin, $"Perm Test {Guid.NewGuid():N}"[..30]);

        var employee = await LoginAsync("employee@ems.local", "Employee123!");
        var response = await employee.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/stock-in",
            new StockMovementRequest(warehouseId, 1, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Warehouse_holding_stock_cannot_be_deleted()
    {
        var client = await LoginAdminAsync();
        var warehouses = await client.GetFromJsonAsync<List<WarehouseDto>>("/api/v1/warehouses");
        var stocked = warehouses!.First(w => w.StockedItemCount > 0);

        var response = await client.DeleteAsync($"/api/v1/warehouses/{stocked.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Item_with_stock_cannot_be_deleted()
    {
        var client = await LoginAdminAsync();
        var warehouseId = await GetWarehouseIdAsync(client);
        var itemId = await CreateItemAsync(client, $"Delete Test {Guid.NewGuid():N}"[..30]);
        await client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/stock-in",
            new StockMovementRequest(warehouseId, 2, null, null));

        var response = await client.DeleteAsync($"/api/v1/inventory/items/{itemId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private Task<HttpClient> LoginAdminAsync() => LoginAsync("admin@ems.local", "Admin123!");

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<int> GetWarehouseIdAsync(HttpClient client)
    {
        var warehouses = await client.GetFromJsonAsync<List<WarehouseDto>>("/api/v1/warehouses");
        return warehouses![0].Id;
    }

    private static async Task<int> CreateItemAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/inventory/items",
            new CreateInventoryItemRequest(name, "Test", "pcs", null, []));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>();
    }
}
