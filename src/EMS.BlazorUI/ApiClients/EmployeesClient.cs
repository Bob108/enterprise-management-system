using System.Net.Http.Json;
using EMS.Shared.Common;
using EMS.Shared.Employees;
using EMS.Shared.Enums;

namespace EMS.BlazorUI.ApiClients;

public sealed class EmployeesClient(HttpClient http)
{
    public Task<PagedResult<EmployeeListItemDto>?> GetPageAsync(
        string? search, int? departmentId, EmploymentStatus? status, int page, int pageSize)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (departmentId is { } d)
        {
            query.Add($"departmentId={d}");
        }

        if (status is { } s)
        {
            query.Add($"status={s}");
        }

        return http.GetFromJsonAsync<PagedResult<EmployeeListItemDto>>(
            $"api/v1/employees?{string.Join("&", query)}");
    }

    public Task<EmployeeDetailDto?> GetByIdAsync(int id)
        => http.GetFromJsonAsync<EmployeeDetailDto>($"api/v1/employees/{id}");

    public async Task<ApiError?> CreateAsync(CreateEmployeeRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/employees", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> UpdateAsync(int id, UpdateEmployeeRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/v1/employees/{id}", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"api/v1/employees/{id}");
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }
}
