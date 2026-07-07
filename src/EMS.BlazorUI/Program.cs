using EMS.BlazorUI;
using EMS.BlazorUI.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddAuthorizationCore();
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

var host = builder.Build();

// Restore the session before first render: if the HttpOnly refresh cookie is present,
// this silently produces a fresh access token and the user never sees the login page.
await host.Services.GetRequiredService<AuthClient>().TryRefreshAsync();

await host.RunAsync();
