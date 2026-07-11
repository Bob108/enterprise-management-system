using System.Net.Http.Json;
using EMS.Shared.Common;
using EMS.Shared.Enums;
using EMS.Shared.Procurement;

namespace EMS.BlazorUI.ApiClients;

public sealed class ProcurementClient(HttpClient http)
{
    // ----- Purchase requests -----

    public Task<PagedResult<PurchaseRequestListItemDto>?> GetRequestsAsync(
        string? search, PurchaseRequestStatus? status, bool mineOnly, int page, int pageSize)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (status is { } s)
        {
            query.Add($"status={s}");
        }

        if (mineOnly)
        {
            query.Add("mineOnly=true");
        }

        return http.GetFromJsonAsync<PagedResult<PurchaseRequestListItemDto>>(
            $"api/v1/procurement/requests?{string.Join("&", query)}");
    }

    public Task<PurchaseRequestDetailDto?> GetRequestAsync(int id)
        => http.GetFromJsonAsync<PurchaseRequestDetailDto>($"api/v1/procurement/requests/{id}");

    public async Task<(int? Id, ApiError? Error)> CreateRequestAsync(SavePurchaseRequestRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/procurement/requests", request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await response.ToApiErrorAsync());
        }

        return (await response.Content.ReadFromJsonAsync<int>(), null);
    }

    public async Task<ApiError?> UpdateRequestAsync(int id, SavePurchaseRequestRequest request)
        => await ToErrorAsync(await http.PutAsJsonAsync($"api/v1/procurement/requests/{id}", request));

    public async Task<ApiError?> SubmitAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/procurement/requests/{id}/submit", null));

    public async Task<ApiError?> ApproveAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/procurement/requests/{id}/approve", null));

    public async Task<ApiError?> RejectAsync(int id, string reason)
        => await ToErrorAsync(await http.PostAsJsonAsync(
            $"api/v1/procurement/requests/{id}/reject", new RejectPurchaseRequestRequest(reason)));

    public async Task<ApiError?> ReturnAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/procurement/requests/{id}/return", null));

    public async Task<ApiError?> CancelRequestAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/procurement/requests/{id}/cancel", null));

    public async Task<(int? OrderId, ApiError? Error)> ConvertAsync(int id, CreatePurchaseOrderRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/v1/procurement/requests/{id}/convert", request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await response.ToApiErrorAsync());
        }

        return (await response.Content.ReadFromJsonAsync<int>(), null);
    }

    // ----- Purchase orders -----

    public Task<PagedResult<PurchaseOrderListItemDto>?> GetOrdersAsync(
        string? search, PurchaseOrderStatus? status, int page, int pageSize)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (status is { } s)
        {
            query.Add($"status={s}");
        }

        return http.GetFromJsonAsync<PagedResult<PurchaseOrderListItemDto>>(
            $"api/v1/procurement/orders?{string.Join("&", query)}");
    }

    public Task<PurchaseOrderDetailDto?> GetOrderAsync(int id)
        => http.GetFromJsonAsync<PurchaseOrderDetailDto>($"api/v1/procurement/orders/{id}");

    public async Task<ApiError?> IssueAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/procurement/orders/{id}/issue", null));

    public async Task<ApiError?> CancelOrderAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/procurement/orders/{id}/cancel", null));

    public async Task<ApiError?> CloseAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/procurement/orders/{id}/close", null));

    public async Task<ApiError?> ReceiveAsync(int id, ReceiveGoodsRequest request)
        => await ToErrorAsync(await http.PostAsJsonAsync($"api/v1/procurement/orders/{id}/receive", request));

    private static async Task<ApiError?> ToErrorAsync(HttpResponseMessage response)
        => response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
}
