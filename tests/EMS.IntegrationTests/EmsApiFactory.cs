using EMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EMS.IntegrationTests;

/// <summary>
/// Boots the real application against a unique LocalDB database per factory instance.
/// Startup runs migrations and seeding (Database:AutoMigrate is on in Development, which
/// is the default WebApplicationFactory environment); the database is dropped on dispose.
/// </summary>
public sealed class EmsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"EMS_Test_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:Default",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};" +
            "Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
    }

    public override async ValueTask DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
            await context.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}
