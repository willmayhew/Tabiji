using System.Net;

namespace Tabiji.Tests;

/// <summary>
/// The account routes the product does not use are not there.
///
/// The framework maps a two-factor surface and a token refresh. Neither is
/// linked from the app, and a cookie-authenticated route that nobody uses is
/// only attack surface, so both answer 404 — before authentication runs, so no
/// caller needs a credential to find out.
/// </summary>
public sealed class UnusedIdentityEndpointTests : IDisposable
{
    private readonly TabijiWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("GET", "/api/auth/manage/info")]
    [InlineData("POST", "/api/auth/manage/info")]
    [InlineData("POST", "/api/auth/manage/2fa")]
    [InlineData("POST", "/api/auth/refresh")]
    public async Task TheRouteDoesNotExist(string method, string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheAccountRoutesTheAppUsesStillExist()
    {
        using var client = _factory.CreateClient();

        foreach (var path in new[] { "/api/auth/session", "/api/auth/csrf" })
        {
            var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
