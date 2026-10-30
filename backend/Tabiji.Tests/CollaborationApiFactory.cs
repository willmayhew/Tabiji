using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Tabiji.Api.Endpoints;

namespace Tabiji.Tests;

/// <summary>
/// The real host with a database of its own and header authentication, so a
/// collaboration test can be a particular seeded account without a cookie.
///
/// The repositories are the real ones: these tests exist to prove the
/// statements and the endpoint mapping together.
/// </summary>
internal sealed class CollaborationApiFactory : TabijiWebApplicationFactory
{
    internal const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiCollaborationApiTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    private const string AntiforgeryHeader = "X-XSRF-TOKEN";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("ConnectionStrings:Tabiji", ConnectionString);
    }

    protected override void ConfigureServices(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
        });
    }

    internal HttpClient CreateClient(Guid userId)
    {
        var client = base.CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(AntiforgeryHeader, AntiforgeryToken(client));

        return client;
    }

    internal HttpClient CreateAnonymousClient() => base.CreateClient();

    private static string AntiforgeryToken(HttpClient client)
    {
        var response = client
            .GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf")
            .GetAwaiter()
            .GetResult();

        Assert.NotNull(response);

        return response.Token;
    }
}
