using System.Net.Http.Json;
using EMS.Shared.Common;
using EMS.Shared.Inventory;

namespace EMS.BlazorUI.ApiClients;

public sealed class InventoryClient(HttpClient http)
{
    public Task<PagedResult<InventoryItemListDto>?> GetPageAsync(
        string? search, bool lowStockOnly, int page, int pageSize)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (lowStockOnly)
        {
            query.Add("lowStockOnly=true");
        }

        return http.GetFromJsonAsync<PagedResult<InventoryItemListDto>>(
            $"api/v1/inventory/items?{string.Join("&", query)}");
    }

    public Task<InventoryItemDetailDto?> GetByIdAsync(int id)
        => http.GetFromJsonAsync<InventoryItemDetailDto>($"api/v1/inventory/items/{id}");

    public Task<List<InventoryTransactionDto>?> GetTransactionsAsync(int id, int take = 50)
        => http.GetFromJsonAsync<List<InventoryTransactionDto>>(
            $"api/v1/inventory/items/{id}/transactions?take={take}");

    public async Task<ApiError?> CreateAsync(CreateInventoryItemRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/inventory/items", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> UpdateAsync(int id, UpdateInventoryItemRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/v1/inventory/items/{id}", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"api/v1/inventory/items/{id}");
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> StockInAsync(int id, StockMovementRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/v1/inventory/items/{id}/stock-in", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> StockOutAsync(int id, StockMovementRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/v1/inventory/items/{id}/stock-out", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> AdjustAsync(int id, StockAdjustmentRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/v1/inventory/items/{id}/adjust", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }
}

public sealed class WarehousesClient(HttpClient http)
{
    public Task<List<WarehouseDto>?> GetAllAsync()
        => http.GetFromJsonAsync<List<WarehouseDto>>("api/v1/warehouses");

    public async Task<ApiError?> CreateAsync(SaveWarehouseRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/warehouses", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> UpdateAsync(int id, SaveWarehouseRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/v1/warehouses/{id}", request);
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }

    public async Task<ApiError?> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"api/v1/warehouses/{id}");
        return response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
    }
}
