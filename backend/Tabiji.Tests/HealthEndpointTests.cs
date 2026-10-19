using System.Net;
using System.Text.Json;

namespace Tabiji.Tests;

public sealed class HealthEndpointTests : IClassFixture<TabijiWebApplicationFactory>
{
    private readonly TabijiWebApplicationFactory _factory;

    public HealthEndpointTests(TabijiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReportsHealthyWithoutTouchingTheDatabase()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
        Assert.Empty(document.RootElement.GetProperty("checks").EnumerateArray());
    }

    [Fact]
    public async Task HealthReady_ReturnsAWellFormedReport()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Unexpected status code: {response.StatusCode}.");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var status = document.RootElement.GetProperty("status").GetString();

        Assert.True(
            status is "Healthy" or "Unhealthy" or "Degraded",
            $"Unexpected health status: {status}.");

        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();

        Assert.Single(checks);
        Assert.Equal("database", checks[0].GetProperty("name").GetString());
    }
}
