using System.Net.Http.Json;

namespace EMS.BlazorUI.ApiClients;

/// <summary>Client-side shape of an RFC 7807 problem response.</summary>
public sealed record ApiError(string Title, Dictionary<string, string[]>? Errors)
{
    public string FullMessage => Errors is { Count: > 0 }
        ? string.Join(" ", Errors.SelectMany(kv => kv.Value))
        : Title;
}

internal static class ApiResponseExtensions
{
    public static async Task<ApiError> ToApiErrorAsync(this HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
            return new ApiError(
                problem?.Title ?? $"Request failed ({(int)response.StatusCode}).",
                problem?.Errors);
        }
        catch (Exception)
        {
            return new ApiError($"Request failed ({(int)response.StatusCode}).", null);
        }
    }

    private sealed record ProblemPayload(string? Title, Dictionary<string, string[]>? Errors);
}
