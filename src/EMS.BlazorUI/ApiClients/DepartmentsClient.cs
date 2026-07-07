using System.Net.Http.Json;
using EMS.Shared.Employees;

namespace EMS.BlazorUI.ApiClients;

public sealed class DepartmentsClient(HttpClient http)
{
    public Task<List<DepartmentDto>?> GetAllAsync()
        => http.GetFromJsonAsync<List<DepartmentDto>>("api/v1/departments");

    public async Task<ApiError?> CreateAsync(SaveDepartmentRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/departments", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> UpdateAsync(int id, SaveDepartmentRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/v1/departments/{id}", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"api/v1/departments/{id}");
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }
}
