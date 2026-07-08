using EMS.BlazorUI;
using EMS.BlazorUI.ApiClients;
using EMS.BlazorUI.Auth;
using EMS.Shared.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
// One client-side policy per permission, mirroring the server's "perm:" prefix.
// UI gating is UX only — the API is the enforcement point (design §9.2).
builder.Services.AddAuthorizationCore(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy($"perm:{permission}",
            policy => policy.RequireClaim(Permissions.ClaimType, permission));
    }
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<AuthTokenStore>();
builder.Services.AddScoped<EmsAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<EmsAuthStateProvider>());
builder.Services.AddScoped(sp => new HttpClient(
    new JwtAuthorizationHandler(sp.GetRequiredService<AuthTokenStore>())
    {
        InnerHandler = new HttpClientHandler(),
    })
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped<AuthClient>();
builder.Services.AddScoped<EmployeesClient>();
builder.Services.AddScoped<DepartmentsClient>();
builder.Services.AddScoped<DesignationsClient>();
builder.Services.AddScoped<AssetsClient>();
builder.Services.AddScoped<AssetCategoriesClient>();
builder.Services.AddScoped<SuppliersClient>();
builder.Services.AddScoped<InventoryClient>();
builder.Services.AddScoped<WarehousesClient>();

var host = builder.Build();

// Restore the session before first render: if the HttpOnly refresh cookie is present,
// this silently produces a fresh access token and the user never sees the login page.
await host.Services.GetRequiredService<AuthClient>().TryRefreshAsync();

await host.RunAsync();
