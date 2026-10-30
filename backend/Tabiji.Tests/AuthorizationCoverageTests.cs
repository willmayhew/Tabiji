using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Tabiji.Tests;

/// <summary>
/// The promise that every endpoint demands an account unless it names itself
/// public, checked against the routing table the host actually built.
///
/// This is the regression test for the security hole that is easiest to
/// create: a route added later, under a trip, that nobody remembered to
/// protect. The failure message names the offending endpoint.
/// </summary>
public sealed class AuthorizationCoverageTests : IDisposable
{
    private readonly TabijiWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void EveryEndpointRequiresAnAccountOrIsExplicitlyPublic()
    {
        var unprotected = Endpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(Describe)
            .ToArray();

        Assert.Empty(unprotected);
    }

    [Fact]
    public void EveryTripEndpointRequiresAnAccount()
    {
        var trips = Endpoints()
            .Where(endpoint => Route(endpoint).StartsWith("/api/trips", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(trips);

        Assert.All(trips, endpoint =>
        {
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>());
        });
    }

    [Fact]
    public void ThePublicSurfaceIsTheAccountRoutesTheProbesAndTheDocument()
    {
        var anonymous = Endpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Route)
            .ToArray();

        Assert.Contains("/api/auth/register", anonymous);
        Assert.Contains("/api/auth/login", anonymous);
        Assert.Contains("/api/auth/session", anonymous);
        Assert.Contains("/api/auth/csrf", anonymous);
        Assert.Contains("/api/auth/logout", anonymous);
        Assert.Contains("/health", anonymous);
        Assert.Contains("/health/ready", anonymous);

        // Nothing on or under a trip may answer without an account.
        Assert.DoesNotContain(
            anonymous,
            route => route.StartsWith("/api/trips", StringComparison.Ordinal));
    }

    private RouteEndpoint[] Endpoints()
    {
        // Creating a client starts the host, which builds the routing table.
        using var client = _factory.CreateClient();

        return [.. _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];
    }

    private static string Route(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText ?? endpoint.DisplayName ?? "(unnamed)";

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

        return $"{string.Join(", ", methods)} {Route(endpoint)}";
    }
}
