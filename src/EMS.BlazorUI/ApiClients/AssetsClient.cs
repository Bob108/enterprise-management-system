using System.Net.Http.Json;
using EMS.Shared.Assets;
using EMS.Shared.Common;
using EMS.Shared.Enums;

namespace EMS.BlazorUI.ApiClients;

public sealed class AssetsClient(HttpClient http)
{
    public Task<PagedResult<AssetListItemDto>?> GetPageAsync(
        string? search, int? categoryId, int? departmentId, AssetStatus? status, int page, int pageSize)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (categoryId is { } c)
        {
            query.Add($"categoryId={c}");
        }

        if (departmentId is { } d)
        {
            query.Add($"departmentId={d}");
        }

        if (status is { } s)
        {
            query.Add($"status={s}");
        }

        return http.GetFromJsonAsync<PagedResult<AssetListItemDto>>(
            $"api/v1/assets?{string.Join("&", query)}");
    }

    public Task<AssetDetailDto?> GetByIdAsync(int id)
        => http.GetFromJsonAsync<AssetDetailDto>($"api/v1/assets/{id}");

    /// <summary>Fetched with the bearer token and rendered as a data URL (an &lt;img src&gt; carries no Authorization header).</summary>
    public async Task<string?> GetQrCodeDataUrlAsync(int id)
    {
        var response = await http.GetAsync($"api/v1/assets/{id}/qrcode");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task<(int? Id, ApiError? Error)> RegisterAsync(RegisterAssetRequest request)
    {
        var response = await http.PostAsJsonAsync("api/v1/assets", request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await response.ToApiErrorAsync());
        }

        return (await response.Content.ReadFromJsonAsync<int>(), null);
    }

    public async Task<ApiError?> UpdateAsync(int id, UpdateAssetRequest request)
        => await ToErrorAsync(await http.PutAsJsonAsync($"api/v1/assets/{id}", request));

    public async Task<ApiError?> AssignAsync(int id, AssignAssetRequest request)
        => await ToErrorAsync(await http.PostAsJsonAsync($"api/v1/assets/{id}/assign", request));

    public async Task<ApiError?> ReturnAsync(int id, ReturnAssetRequest request)
        => await ToErrorAsync(await http.PostAsJsonAsync($"api/v1/assets/{id}/return", request));

    public async Task<ApiError?> TransferAsync(int id, TransferAssetRequest request)
        => await ToErrorAsync(await http.PostAsJsonAsync($"api/v1/assets/{id}/transfer", request));

    public async Task<ApiError?> MarkUnderRepairAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/assets/{id}/repair", null));

    public async Task<ApiError?> MarkRepairedAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/assets/{id}/repaired", null));

    public async Task<ApiError?> ReportLostAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/assets/{id}/report-lost", null));

    public async Task<ApiError?> RecoverAsync(int id)
        => await ToErrorAsync(await http.PostAsync($"api/v1/assets/{id}/recover", null));

    public async Task<ApiError?> DisposeAsync(int id, DisposeAssetRequest request)
        => await ToErrorAsync(await http.PostAsJsonAsync($"api/v1/assets/{id}/dispose", request));

    private static async Task<ApiError?> ToErrorAsync(HttpResponseMessage response)
        => response.IsSuccessStatusCode ? null : await response.ToApiErrorAsync();
}
