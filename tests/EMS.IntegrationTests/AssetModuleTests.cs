using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EMS.Shared.Assets;
using EMS.Shared.Auth;
using EMS.Shared.Common;
using EMS.Shared.Employees;
using EMS.Shared.Enums;
using FluentAssertions;

namespace EMS.IntegrationTests;

public class AssetModuleTests(EmsApiFactory factory) : IClassFixture<EmsApiFactory>
{
    [Fact]
    public async Task Assets_list_returns_seeded_page()
    {
        var client = await LoginAdminAsync();

        var page = await client.GetFromJsonAsync<PagedResult<AssetListItemDto>>(
            "/api/v1/assets?page=1&pageSize=50");

        page!.TotalCount.Should().BeGreaterThanOrEqualTo(24);
        page.Items.Should().Contain(a => a.Status == AssetStatus.Assigned);
        page.Items.Should().Contain(a => a.Status == AssetStatus.Retired);
    }

    [Fact]
    public async Task Full_lifecycle_register_assign_return_dispose()
    {
        var client = await LoginAdminAsync();
        var (categoryId, departmentId) = await GetReferenceIdsAsync(client);
        var employees = await client.GetFromJsonAsync<PagedResult<EmployeeListItemDto>>(
            "/api/v1/employees?page=1&pageSize=1");
        var employeeId = employees!.Items[0].Id;

        // Register
        var register = await client.PostAsJsonAsync("/api/v1/assets", new RegisterAssetRequest(
            "Lifecycle Test Laptop", categoryId, departmentId, null, "LC-TEST-001", null,
            new DateOnly(2025, 6, 1), 90_000m, null, null));
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await register.Content.ReadFromJsonAsync<int>();

        // Assign
        var assign = await client.PostAsJsonAsync($"/api/v1/assets/{id}/assign",
            new AssignAssetRequest(employeeId, "brand new"));
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterAssign = await client.GetFromJsonAsync<AssetDetailDto>($"/api/v1/assets/{id}");
        afterAssign!.Status.Should().Be(AssetStatus.Assigned);
        afterAssign.Assignments.Should().ContainSingle(a => a.ReturnedOn == null);

        // Disposing while assigned violates the state machine → 409.
        var badDispose = await client.PostAsJsonAsync($"/api/v1/assets/{id}/dispose",
            new DisposeAssetRequest(DisposalMethod.Scrapped, null, null));
        badDispose.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Return, then dispose legally.
        var ret = await client.PostAsJsonAsync($"/api/v1/assets/{id}/return",
            new ReturnAssetRequest("returned fine"));
        ret.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dispose = await client.PostAsJsonAsync($"/api/v1/assets/{id}/dispose",
            new DisposeAssetRequest(DisposalMethod.Sold, 50_000m, "sold to staff"));
        dispose.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var final = await client.GetFromJsonAsync<AssetDetailDto>($"/api/v1/assets/{id}");
        final!.Status.Should().Be(AssetStatus.Retired);
        final.Disposal.Should().NotBeNull();
        final.Disposal!.Method.Should().Be(DisposalMethod.Sold);
        final.Assignments.Single().ReturnedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task Transfer_moves_department_and_appears_in_history()
    {
        var client = await LoginAdminAsync();
        var (categoryId, departmentId) = await GetReferenceIdsAsync(client);
        var departments = await client.GetFromJsonAsync<List<DepartmentDto>>("/api/v1/departments");
        var target = departments!.First(d => d.Id != departmentId);

        var register = await client.PostAsJsonAsync("/api/v1/assets", new RegisterAssetRequest(
            "Transfer Test Printer", categoryId, departmentId, null, null, null,
            new DateOnly(2025, 5, 1), 45_000m, null, null));
        var id = await register.Content.ReadFromJsonAsync<int>();

        var transfer = await client.PostAsJsonAsync($"/api/v1/assets/{id}/transfer",
            new TransferAssetRequest(target.Id, "test move"));
        transfer.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await client.GetFromJsonAsync<AssetDetailDto>($"/api/v1/assets/{id}");
        detail!.DepartmentId.Should().Be(target.Id);
        detail.Transfers.Should().ContainSingle(t => t.ToDepartment == target.Name);
    }

    [Fact]
    public async Task Depreciation_posting_is_idempotent()
    {
        var client = await LoginAdminAsync();

        var first = await client.PostAsync("/api/v1/admin/depreciation/2025/1", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstCount = await first.Content.ReadFromJsonAsync<int>();
        firstCount.Should().BeGreaterThan(0); // seeded 2023/2024 assets are eligible

        var second = await client.PostAsync("/api/v1/admin/depreciation/2025/1", null);
        var secondCount = await second.Content.ReadFromJsonAsync<int>();
        secondCount.Should().Be(0); // same month again → nothing new

        // Posted depreciation is reflected in book value.
        var page = await client.GetFromJsonAsync<PagedResult<AssetListItemDto>>(
            "/api/v1/assets?page=1&pageSize=50");
        page!.Items.Should().Contain(a => a.BookValue < a.PurchaseCost);
    }

    [Fact]
    public async Task Qr_code_endpoint_returns_png()
    {
        var client = await LoginAdminAsync();
        var page = await client.GetFromJsonAsync<PagedResult<AssetListItemDto>>(
            "/api/v1/assets?page=1&pageSize=1");

        var response = await client.GetAsync($"/api/v1/assets/{page!.Items[0].Id}/qrcode");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes[..4].Should().Equal(0x89, (byte)'P', (byte)'N', (byte)'G');
    }

    [Fact]
    public async Task Employee_role_cannot_register_assets()
    {
        var client = await LoginAsync("employee@ems.local", "Employee123!");

        var response = await client.PostAsJsonAsync("/api/v1/assets", new RegisterAssetRequest(
            "Sneaky Asset", 1, 1, null, null, null, new DateOnly(2025, 1, 1), 1_000m, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Category_with_assets_cannot_be_deleted()
    {
        var client = await LoginAdminAsync();
        var categories = await client.GetFromJsonAsync<List<AssetCategoryDto>>("/api/v1/asset-categories");
        var populated = categories!.First(c => c.AssetCount > 0);

        var response = await client.DeleteAsync($"/api/v1/asset-categories/{populated.Id}");

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

    private static async Task<(int CategoryId, int DepartmentId)> GetReferenceIdsAsync(HttpClient client)
    {
        var categories = await client.GetFromJsonAsync<List<AssetCategoryDto>>("/api/v1/asset-categories");
        var departments = await client.GetFromJsonAsync<List<DepartmentDto>>("/api/v1/departments");
        return (categories![0].Id, departments![0].Id);
    }
}
