using System.Net.Http.Json;
using EMS.Shared.Assets;

namespace EMS.BlazorUI.ApiClients;

public sealed class AssetCategoriesClient(HttpClient http)
{
    public Task<List<AssetCategoryDto>?> GetAllAsync()
        => http.GetFromJsonAsync<List<AssetCategoryDto>>("api/v1/asset-categories");

    public async Task<ApiError?> CreateAsync(SaveAssetCategoryRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/asset-categories", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> UpdateAsync(int id, SaveAssetCategoryRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/v1/asset-categories/{id}", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"api/v1/asset-categories/{id}");
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }
}

public sealed class SuppliersClient(HttpClient http)
{
    public Task<List<SupplierDto>?> GetAllAsync()
        => http.GetFromJsonAsync<List<SupplierDto>>("api/v1/suppliers");

    public async Task<ApiError?> CreateAsync(SaveSupplierRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/suppliers", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> UpdateAsync(int id, SaveSupplierRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/v1/suppliers/{id}", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"api/v1/suppliers/{id}");
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }
}
