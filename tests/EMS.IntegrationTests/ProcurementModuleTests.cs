using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EMS.Shared.Assets;
using EMS.Shared.Auth;
using EMS.Shared.Common;
using EMS.Shared.Employees;
using EMS.Shared.Enums;
using EMS.Shared.Inventory;
using EMS.Shared.Procurement;
using FluentAssertions;

namespace EMS.IntegrationTests;

public class ProcurementModuleTests(EmsApiFactory factory) : IClassFixture<EmsApiFactory>
{
    [Fact]
    public async Task Full_flow_pr_to_po_to_grn_creates_assets_and_posts_stock()
    {
        var admin = await LoginAdminAsync();
        var employee = await LoginAsync("employee@ems.local", "Employee123!");
        var refs = await GetReferenceDataAsync(admin);

        // Employee raises a mixed request (below the 100k threshold → single approval).
        var create = await employee.PostAsJsonAsync("/api/v1/procurement/requests",
            new SavePurchaseRequestRequest(refs.DepartmentId, "New starter kit",
            [
                new SavePurchaseRequestLine("Test Laptop Pro 14", ItemNature.Asset, refs.AssetCategoryId, null, 1, 40_000m),
                new SavePurchaseRequestLine("HP 26A Printer Toner", ItemNature.Consumable, null, refs.InventoryItemId, 5, 9_500m),
            ]));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var prId = await create.Content.ReadFromJsonAsync<int>();

        (await employee.PostAsync($"/api/v1/procurement/requests/{prId}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Admin (not the requester) approves — single level suffices below the threshold.
        (await admin.PostAsync($"/api/v1/procurement/requests/{prId}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var approved = await admin.GetFromJsonAsync<PurchaseRequestDetailDto>(
            $"/api/v1/procurement/requests/{prId}");
        approved!.Status.Should().Be(PurchaseRequestStatus.Approved);

        // Convert to an order with agreed prices.
        var convert = await admin.PostAsJsonAsync($"/api/v1/procurement/requests/{prId}/convert",
            new CreatePurchaseOrderRequest(refs.SupplierId, null, null,
                approved.Lines.Select(l => new CreatePurchaseOrderLine(
                    l.Id!.Value, l.Description.Contains("Laptop") ? 42_000m : 9_000m)).ToList()));
        convert.StatusCode.Should().Be(HttpStatusCode.OK);
        var poId = await convert.Content.ReadFromJsonAsync<int>();

        var converted = await admin.GetFromJsonAsync<PurchaseRequestDetailDto>(
            $"/api/v1/procurement/requests/{prId}");
        converted!.Status.Should().Be(PurchaseRequestStatus.Converted);
        converted.PurchaseOrderId.Should().Be(poId);

        // Record stock before receiving so the assertion is delta-based.
        var itemBefore = await admin.GetFromJsonAsync<InventoryItemDetailDto>(
            $"/api/v1/inventory/items/{refs.InventoryItemId}");
        var stockBefore = itemBefore!.StockLevels
            .SingleOrDefault(s => s.WarehouseId == refs.WarehouseId)?.Quantity ?? 0;

        // Issue, then receive everything.
        (await admin.PostAsync($"/api/v1/procurement/orders/{poId}/issue", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var order = await admin.GetFromJsonAsync<PurchaseOrderDetailDto>($"/api/v1/procurement/orders/{poId}");
        var receive = await admin.PostAsJsonAsync($"/api/v1/procurement/orders/{poId}/receive",
            new ReceiveGoodsRequest(refs.WarehouseId, "all in",
                order!.Lines.Select(l => new ReceiveGoodsLine(l.Id, l.OrderedQuantity)).ToList()));
        receive.StatusCode.Should().Be(HttpStatusCode.OK);

        // --- The integration moment, verified end to end ---
        var received = await admin.GetFromJsonAsync<PurchaseOrderDetailDto>($"/api/v1/procurement/orders/{poId}");
        received!.Status.Should().Be(PurchaseOrderStatus.FullyReceived);
        received.Grns.Should().ContainSingle();
        var grnNumber = received.Grns[0].GrnNumber;

        // 1) The asset line became a real asset with purchase details from the order.
        var assets = await admin.GetFromJsonAsync<PagedResult<AssetListItemDto>>(
            "/api/v1/assets?search=Test Laptop Pro 14&page=1&pageSize=10");
        assets!.Items.Should().ContainSingle();
        assets.Items[0].PurchaseCost.Should().Be(42_000m);
        assets.Items[0].Status.Should().Be(AssetStatus.Available);

        // 2) The consumable line posted into the inventory ledger and moved the balance.
        var itemAfter = await admin.GetFromJsonAsync<InventoryItemDetailDto>(
            $"/api/v1/inventory/items/{refs.InventoryItemId}");
        itemAfter!.StockLevels.Single(s => s.WarehouseId == refs.WarehouseId)
            .Quantity.Should().Be(stockBefore + 5);

        var ledger = await admin.GetFromJsonAsync<List<InventoryTransactionDto>>(
            $"/api/v1/inventory/items/{refs.InventoryItemId}/transactions");
        ledger!.Should().Contain(t =>
            t.Type == InventoryTransactionType.GrnReceipt
            && t.QuantityChange == 5
            && t.Reference == grnNumber);
    }

    [Fact]
    public async Task Approver_cannot_approve_their_own_request()
    {
        var admin = await LoginAdminAsync();
        var refs = await GetReferenceDataAsync(admin);

        var prId = await CreateAndSubmitAsync(admin, refs, 10_000m);

        var response = await admin.PostAsync($"/api/v1/procurement/requests/{prId}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("own");
    }

    [Fact]
    public async Task High_value_request_needs_a_second_distinct_approver()
    {
        var admin = await LoginAdminAsync();
        var employee = await LoginAsync("employee@ems.local", "Employee123!");
        var refs = await GetReferenceDataAsync(admin);

        var prId = await CreateAndSubmitAsync(employee, refs, 150_000m);

        // L1 by admin succeeds, but the request stays submitted awaiting L2…
        (await admin.PostAsync($"/api/v1/procurement/requests/{prId}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterL1 = await admin.GetFromJsonAsync<PurchaseRequestDetailDto>(
            $"/api/v1/procurement/requests/{prId}");
        afterL1!.Status.Should().Be(PurchaseRequestStatus.Submitted);
        afterL1.RequiresSecondApproval.Should().BeTrue();

        // …and the same person cannot provide the second signature.
        var again = await admin.PostAsync($"/api/v1/procurement/requests/{prId}/approve", null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("different approver");
    }

    [Fact]
    public async Task Employee_cannot_convert_or_issue()
    {
        var employee = await LoginAsync("employee@ems.local", "Employee123!");
        var admin = await LoginAdminAsync();
        var refs = await GetReferenceDataAsync(admin);

        var convert = await employee.PostAsJsonAsync("/api/v1/procurement/requests/1/convert",
            new CreatePurchaseOrderRequest(refs.SupplierId, null, null, []));
        convert.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var issue = await employee.PostAsync("/api/v1/procurement/orders/1/issue", null);
        issue.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Partial_receipt_transitions_and_over_receipt_is_rejected()
    {
        var admin = await LoginAdminAsync();
        var employee = await LoginAsync("employee@ems.local", "Employee123!");
        var refs = await GetReferenceDataAsync(admin);

        // 10 consumables through the flow.
        var create = await employee.PostAsJsonAsync("/api/v1/procurement/requests",
            new SavePurchaseRequestRequest(refs.DepartmentId, null,
                [new SavePurchaseRequestLine("Toner restock", ItemNature.Consumable, null, refs.InventoryItemId, 10, 9_000m)]));
        var prId = await create.Content.ReadFromJsonAsync<int>();
        await employee.PostAsync($"/api/v1/procurement/requests/{prId}/submit", null);
        await admin.PostAsync($"/api/v1/procurement/requests/{prId}/approve", null);

        var detail = await admin.GetFromJsonAsync<PurchaseRequestDetailDto>(
            $"/api/v1/procurement/requests/{prId}");
        var convert = await admin.PostAsJsonAsync($"/api/v1/procurement/requests/{prId}/convert",
            new CreatePurchaseOrderRequest(refs.SupplierId, null, null,
                [new CreatePurchaseOrderLine(detail!.Lines[0].Id!.Value, 8_800m)]));
        var poId = await convert.Content.ReadFromJsonAsync<int>();
        await admin.PostAsync($"/api/v1/procurement/orders/{poId}/issue", null);

        var order = await admin.GetFromJsonAsync<PurchaseOrderDetailDto>($"/api/v1/procurement/orders/{poId}");
        var lineId = order!.Lines[0].Id;

        // Receive 4 → partially received.
        (await admin.PostAsJsonAsync($"/api/v1/procurement/orders/{poId}/receive",
            new ReceiveGoodsRequest(refs.WarehouseId, null, [new ReceiveGoodsLine(lineId, 4)])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetFromJsonAsync<PurchaseOrderDetailDto>($"/api/v1/procurement/orders/{poId}"))!
            .Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);

        // 7 more would exceed the outstanding 6 → 409.
        (await admin.PostAsJsonAsync($"/api/v1/procurement/orders/{poId}/receive",
            new ReceiveGoodsRequest(refs.WarehouseId, null, [new ReceiveGoodsLine(lineId, 7)])))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The exact outstanding 6 completes the order.
        (await admin.PostAsJsonAsync($"/api/v1/procurement/orders/{poId}/receive",
            new ReceiveGoodsRequest(refs.WarehouseId, null, [new ReceiveGoodsLine(lineId, 6)])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetFromJsonAsync<PurchaseOrderDetailDto>($"/api/v1/procurement/orders/{poId}"))!
            .Status.Should().Be(PurchaseOrderStatus.FullyReceived);
    }

    [Fact]
    public async Task Employee_without_view_permission_sees_only_their_own_requests()
    {
        var admin = await LoginAdminAsync();
        var employee = await LoginAsync("employee@ems.local", "Employee123!");
        var refs = await GetReferenceDataAsync(admin);

        // One request from each of them.
        await CreateAsync(admin, refs, 5_000m);
        await CreateAsync(employee, refs, 5_000m);

        var page = await employee.GetFromJsonAsync<PagedResult<PurchaseRequestListItemDto>>(
            "/api/v1/procurement/requests?page=1&pageSize=50");

        page!.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(r => r.RequestedByName == "Demo Employee");
    }

    // ----- helpers -----

    private sealed record ReferenceData(
        int DepartmentId, int AssetCategoryId, int InventoryItemId, int SupplierId, int WarehouseId);

    private static async Task<ReferenceData> GetReferenceDataAsync(HttpClient admin)
    {
        var departments = await admin.GetFromJsonAsync<List<DepartmentDto>>("/api/v1/departments");
        var categories = await admin.GetFromJsonAsync<List<AssetCategoryDto>>("/api/v1/asset-categories");
        var items = await admin.GetFromJsonAsync<PagedResult<InventoryItemListDto>>(
            "/api/v1/inventory/items?page=1&pageSize=1");
        var suppliers = await admin.GetFromJsonAsync<List<SupplierDto>>("/api/v1/suppliers");
        var warehouses = await admin.GetFromJsonAsync<List<WarehouseDto>>("/api/v1/warehouses");
        return new ReferenceData(
            departments![0].Id, categories![0].Id, items!.Items[0].Id, suppliers![0].Id, warehouses![0].Id);
    }

    private static async Task<int> CreateAsync(HttpClient client, ReferenceData refs, decimal unitCost)
    {
        var response = await client.PostAsJsonAsync("/api/v1/procurement/requests",
            new SavePurchaseRequestRequest(refs.DepartmentId, null,
                [new SavePurchaseRequestLine("Test line", ItemNature.Asset, refs.AssetCategoryId, null, 1, unitCost)]));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>();
    }

    private static async Task<int> CreateAndSubmitAsync(HttpClient client, ReferenceData refs, decimal unitCost)
    {
        var id = await CreateAsync(client, refs, unitCost);
        (await client.PostAsync($"/api/v1/procurement/requests/{id}/submit", null)).EnsureSuccessStatusCode();
        return id;
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
}
