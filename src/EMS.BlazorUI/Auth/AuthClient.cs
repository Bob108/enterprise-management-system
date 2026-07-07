using System.Net;
using System.Net.Http.Json;
using EMS.Shared.Auth;

namespace EMS.BlazorUI.Auth;

/// <summary>
/// Client half of the auth flow: access token lives in <see cref="AuthTokenStore"/>,
/// the refresh token only ever exists as an HttpOnly cookie the browser attaches to
/// /api/v1/auth requests automatically.
/// </summary>
public sealed class AuthClient(HttpClient http, AuthTokenStore store, EmsAuthStateProvider stateProvider)
{
    /// <summary>Returns null on success, otherwise a user-displayable error message.</summary>
    public async Task<string?> LoginAsync(string email, string password)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/v1/auth/login", new LoginRequest(email, password));
        }
        catch (HttpRequestException)
        {
            return "Cannot reach the server. Check your connection and try again.";
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Invalid email or password.";
        }

        if (!response.IsSuccessStatusCode)
        {
            return "Sign-in failed — please try again.";
        }

        var session = await response.Content.ReadFromJsonAsync<AuthResponse>();
        store.Set(session!);
        stateProvider.NotifyChanged();
        return null;
    }

    /// <summary>Silently restores the session from the refresh cookie; false if none/expired.</summary>
    public async Task<bool> TryRefreshAsync()
    {
        try
        {
            var response = await http.PostAsync("api/v1/auth/refresh", content: null);
            if (!response.IsSuccessStatusCode)
            {
                store.Clear();
                return false;
            }

            var session = await response.Content.ReadFromJsonAsync<AuthResponse>();
            store.Set(session!);
            stateProvider.NotifyChanged();
            return true;
        }
        catch (HttpRequestException)
        {
            store.Clear();
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await http.PostAsync("api/v1/auth/logout", content: null);
        }
        catch (HttpRequestException)
        {
            // Server revocation failed (offline?) — local sign-out proceeds regardless.
        }

        store.Clear();
        stateProvider.NotifyChanged();
    }
}
