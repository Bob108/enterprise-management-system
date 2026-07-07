using System.Net.Http.Headers;

namespace EMS.BlazorUI.Auth;

/// <summary>Attaches the in-memory access token to every outgoing API request.</summary>
public sealed class JwtAuthorizationHandler(AuthTokenStore store) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (store.AccessToken is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
