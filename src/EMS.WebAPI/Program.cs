using System.Text;
using EMS.Application;
using EMS.Application.Common.Interfaces;
using EMS.Infrastructure;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Persistence;
using EMS.WebAPI.Authorization;
using EMS.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // preserveStaticLogger keeps each host's logger independent so multiple hosts can
    // coexist in one process (WebApplicationFactory boots several in integration tests).
    builder.Host.UseSerilog(
        (context, configuration) => configuration.ReadFrom.Configuration(context.Configuration),
        preserveStaticLogger: true);

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
    var jwtKey = jwtSection["Key"]
        ?? throw new InvalidOperationException(
            "Jwt:Key is not configured. Dev: appsettings.Development.json; prod: Azure Key Vault.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSection["Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    var app = builder.Build();

    // Dev/test convenience only — production applies the pipeline's idempotent
    // migration script instead (design §13).
    if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<EmsDbInitializer>();
        await initializer.InitializeAsync();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<EMS.WebAPI.Middleware.ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseWebAssemblyDebugging();
    }

    app.UseHttpsRedirection();

    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");
    app.MapFallbackToFile("index.html");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is EF Core's design-time host shutdown — not a failure.
    Log.Fatal(ex, "EMS.WebAPI terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Exposes the entry point to WebApplicationFactory-based integration tests.
public partial class Program;
