using System.Net;
using FluentAssertions;

namespace EMS.IntegrationTests;

public class HealthEndpointTests(EmsApiFactory factory) : IClassFixture<EmsApiFactory>
{
    [Fact]
    public async Task Health_endpoint_returns_healthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
