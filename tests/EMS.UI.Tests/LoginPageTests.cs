using Bunit;
using EMS.BlazorUI.Auth;
using EMS.BlazorUI.Pages;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace EMS.UI.Tests;

public class LoginPageTests : BunitContext
{
    public LoginPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddScoped<AuthTokenStore>();
        Services.AddScoped<EmsAuthStateProvider>();
        Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost/") });
        Services.AddScoped<AuthClient>();
    }

    [Fact]
    public void Login_page_renders_credential_fields_and_submit_button()
    {
        var cut = Render<Login>();

        Assert.True(cut.FindAll("input").Count >= 2, "expected email and password inputs");
        Assert.Contains("Sign in", cut.Find("button").TextContent);
        Assert.Contains("admin@ems.local", cut.Markup); // demo credentials hint
    }

    [Fact]
    public void Login_page_shows_validation_error_when_fields_empty()
    {
        var cut = Render<Login>();

        cut.Find("button").Click();

        Assert.Contains("Enter your email and password.", cut.Markup);
    }
}
