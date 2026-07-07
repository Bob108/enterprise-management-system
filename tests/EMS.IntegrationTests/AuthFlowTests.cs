using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EMS.Shared.Auth;
using EMS.Shared.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EMS.IntegrationTests;

public class AuthFlowTests(EmsApiFactory factory) : IClassFixture<EmsApiFactory>
{
    private const string AdminEmail = "admin@ems.local";
    private const string AdminPassword = "Admin123!";
    private const string EmployeeEmail = "employee@ems.local";
    private const string EmployeePassword = "Employee123!";

    [Fact]
    public async Task Login_returns_token_roles_and_permissions_for_seeded_admin()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(AdminEmail, AdminPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeNullOrEmpty();
        auth.Roles.Should().Contain("Super Admin");
        auth.Permissions.Should().Contain(Permissions.Users.View);

        // Refresh token travels only as an HttpOnly cookie, never in the body.
        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("ems_refresh="));
    }

    [Fact]
    public async Task Login_fails_with_wrong_password()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(AdminEmail, "WrongPassword1!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Users_list_is_allowed_for_admin_with_users_view_permission()
    {
        var client = await CreateAuthenticatedClientAsync(AdminEmail, AdminPassword);

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        users.Should().Contain(u => u.Email == AdminEmail);
    }

    [Fact]
    public async Task Users_list_is_forbidden_for_employee_without_permission()
    {
        var client = await CreateAuthenticatedClientAsync(EmployeeEmail, EmployeePassword);

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_reuse_revokes_the_whole_family()
    {
        // Manual cookie handling so we can deliberately replay an old token.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(AdminEmail, AdminPassword));
        var firstToken = ExtractRefreshCookie(loginResponse);

        // First refresh with the original token succeeds and rotates.
        var firstRefresh = await RefreshWithCookieAsync(client, firstToken);
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondToken = ExtractRefreshCookie(firstRefresh);
        secondToken.Should().NotBe(firstToken);

        // Replaying the consumed token is theft detection → 401 …
        var replay = await RefreshWithCookieAsync(client, firstToken);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // … and the whole family is revoked, so the newest token is dead too.
        var afterRevocation = await RefreshWithCookieAsync(client, secondToken);
        afterRevocation.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> RefreshWithCookieAsync(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"ems_refresh={refreshToken}");
        return client.SendAsync(request);
    }

    private static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("ems_refresh="));
        return setCookie["ems_refresh=".Length..setCookie.IndexOf(';')];
    }
}
