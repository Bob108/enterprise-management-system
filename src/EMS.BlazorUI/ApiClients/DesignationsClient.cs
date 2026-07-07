using System.Net.Http.Json;
using EMS.Shared.Employees;

namespace EMS.BlazorUI.ApiClients;

public sealed class DesignationsClient(HttpClient http)
{
    public Task<List<DesignationDto>?> GetAllAsync()
        => http.GetFromJsonAsync<List<DesignationDto>>("api/v1/designations");

    public async Task<ApiError?> CreateAsync(SaveDesignationRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/designations", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> UpdateAsync(int id, SaveDesignationRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/v1/designations/{id}", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"api/v1/designations/{id}");
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }
}
