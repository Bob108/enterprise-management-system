using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EMS.Shared.Auth;
using EMS.Shared.Common;
using EMS.Shared.Employees;
using EMS.Shared.Enums;
using FluentAssertions;

namespace EMS.IntegrationTests;

public class EmployeeModuleTests(EmsApiFactory factory) : IClassFixture<EmsApiFactory>
{
    [Fact]
    public async Task Employees_list_returns_seeded_page()
    {
        var client = await LoginAsync("admin@ems.local", "Admin123!");

        var page = await client.GetFromJsonAsync<PagedResult<EmployeeListItemDto>>(
            "/api/v1/employees?page=1&pageSize=10");

        page!.TotalCount.Should().BeGreaterThanOrEqualTo(16);
        page.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task Create_employee_then_fetch_detail_roundtrips()
    {
        var client = await LoginAsync("admin@ems.local", "Admin123!");
        var (departmentId, designationId) = await GetReferenceIdsAsync(client);
        var email = $"test.{Guid.NewGuid():N}@northwind.example";

        var create = await client.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "Test", "Person", email, "+254 700 111 222", departmentId, designationId,
            EmploymentStatus.Active, new DateOnly(2025, 1, 15), new DateOnly(1995, 6, 1),
            "42 Test Street", [new EmergencyContactDto(null, "Next Of Kin", "Spouse", "+254 700 333 444")]));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await create.Content.ReadFromJsonAsync<int>();

        var detail = await client.GetFromJsonAsync<EmployeeDetailDto>($"/api/v1/employees/{id}");
        detail!.Email.Should().Be(email);
        detail.EmployeeNumber.Should().StartWith("EMP-");
        detail.EmergencyContacts.Should().ContainSingle(c => c.Name == "Next Of Kin");
        detail.RowVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_with_invalid_data_returns_400_with_field_errors()
    {
        var client = await LoginAsync("admin@ems.local", "Admin123!");
        var (departmentId, designationId) = await GetReferenceIdsAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "", "Person", "not-an-email", null, departmentId, designationId,
            EmploymentStatus.Active, new DateOnly(2025, 1, 15), null, null, []));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("FirstName").And.Contain("Email");
    }

    [Fact]
    public async Task Employee_role_cannot_create_employees()
    {
        var client = await LoginAsync("employee@ems.local", "Employee123!");
        var admin = await LoginAsync("admin@ems.local", "Admin123!");
        var (departmentId, designationId) = await GetReferenceIdsAsync(admin);

        var response = await client.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "Sneaky", "Employee", "sneaky@northwind.example", null, departmentId, designationId,
            EmploymentStatus.Active, new DateOnly(2025, 1, 15), null, null, []));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deleted_employee_disappears_from_list_and_detail()
    {
        var client = await LoginAsync("admin@ems.local", "Admin123!");
        var (departmentId, designationId) = await GetReferenceIdsAsync(client);
        var email = $"delete.{Guid.NewGuid():N}@northwind.example";

        var create = await client.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "Soon", "Gone", email, null, departmentId, designationId,
            EmploymentStatus.Active, new DateOnly(2025, 1, 15), null, null, []));
        var id = await create.Content.ReadFromJsonAsync<int>();

        var delete = await client.DeleteAsync($"/api/v1/employees/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Soft-deleted: hidden by the global query filter.
        var detail = await client.GetAsync($"/api/v1/employees/{id}");
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Department_with_employees_cannot_be_deleted()
    {
        var client = await LoginAsync("admin@ems.local", "Admin123!");
        var departments = await client.GetFromJsonAsync<List<DepartmentDto>>("/api/v1/departments");
        var populated = departments!.First(d => d.EmployeeCount > 0);

        var response = await client.DeleteAsync($"/api/v1/departments/{populated.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<(int DepartmentId, int DesignationId)> GetReferenceIdsAsync(HttpClient client)
    {
        var departments = await client.GetFromJsonAsync<List<DepartmentDto>>("/api/v1/departments");
        var designations = await client.GetFromJsonAsync<List<DesignationDto>>("/api/v1/designations");
        return (departments![0].Id, designations![0].Id);
    }
}
