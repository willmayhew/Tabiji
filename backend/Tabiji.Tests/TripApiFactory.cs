using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tabiji.Api.Endpoints;
using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;
using Tabiji.Tests.Fakes;

namespace Tabiji.Tests;

/// <summary>
/// Hosts the real API with the trip repository replaced by an in-memory fake, so the
/// endpoint tests exercise routing, status codes and JSON without needing a database.
///
/// The authentication cookie is replaced by a header, so a test can be a particular
/// account. Everything else about authorization is the real thing: the fallback policy,
/// the group policies, the antiforgery filter and the 401/404 answers.
/// </summary>
internal sealed class TripApiFactory : TabijiWebApplicationFactory
{
    private const string AntiforgeryHeader = "X-XSRF-TOKEN";

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private readonly string? _environment;

    internal FakeTripRepository Repository { get; }

    internal TripApiFactory()
        : this(DefaultSeed)
    {
    }

    internal TripApiFactory(IEnumerable<Trip> seed)
        : this(seed, null)
    {
    }

    /// <summary>
    /// The same host in a chosen environment, for the rules that differ
    /// between Development and Production.
    /// </summary>
    internal TripApiFactory(string environment)
        : this(DefaultSeed, environment)
    {
    }

    private TripApiFactory(IEnumerable<Trip> seed, string? environment)
    {
        Repository = new FakeTripRepository(seed, TestUsers.Owner);
        _environment = environment;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        if (_environment is not null)
        {
            builder.UseEnvironment(_environment);
        }
    }

    /// <summary>
    /// A client acting as <see cref="TestUsers.Owner"/>, carrying the antiforgery
    /// token its writes will be asked for.
    /// </summary>
    public new HttpClient CreateClient() => CreateClient(TestUsers.OwnerId);

    /// <summary>
    /// A client acting as the given account. The token is collected on the same
    /// client, so the antiforgery cookie and the token stay together, and it is
    /// collected as that account because the token embeds the account.
    /// </summary>
    internal HttpClient CreateClient(Guid userId)
    {
        var client = base.CreateClient();

        AddIdentityHeaders(client, userId);

        return client;
    }

    /// <summary>
    /// The same client over a chosen address. Production sets its cookies
    /// <c>Secure</c>, so a test of a Production rule has to speak HTTPS for the
    /// antiforgery cookie to come back.
    /// </summary>
    internal HttpClient CreateClient(Guid userId, Uri baseAddress)
    {
        var client = base.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = baseAddress,
        });

        AddIdentityHeaders(client, userId);

        return client;
    }

    private static void AddIdentityHeaders(HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(AntiforgeryHeader, AntiforgeryToken(client));
    }

    /// <summary>A client with no account at all.</summary>
    internal HttpClient CreateAnonymousClient() => base.CreateClient();

    protected override void ConfigureServices(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITripRepository>();
            services.AddSingleton<ITripRepository>(Repository);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
        });
    }

    private static string AntiforgeryToken(HttpClient client)
    {
        var response = client
            .GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf")
            .GetAwaiter()
            .GetResult();

        Assert.NotNull(response);

        return response.Token;
    }

    /// <summary>
    /// The seed trips. They deliberately travel to more than one country and
    /// cover more than one stop, so the ordering rule, the destination rules
    /// and the loading of the route are all exercised against a set that does
    /// not assume every trip is in Japan or has a single destination.
    ///
    /// Their stops carry dates, so the read path is exercised too, including a
    /// stop with only a start date.
    /// </summary>
    private static Trip[] DefaultSeed =>
    [
        Trip.Create(
            "Kyoto",
            [
                new TripDestinationDraft(null, "Kyoto", new DateOnly(2027, 4, 10), new DateOnly(2027, 4, 12)),
                new TripDestinationDraft(null, "Nara", new DateOnly(2027, 4, 12), new DateOnly(2027, 4, 13)),
                new TripDestinationDraft(null, "Osaka", new DateOnly(2027, 4, 14), null)
            ],
            new DateOnly(2027, 4, 10),
            new DateOnly(2027, 4, 14),
            CreatedAt),
        Trip.Create(
            "Rome",
            [
                new TripDestinationDraft(null, "Rome", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 4)),
                new TripDestinationDraft(null, "Amalfi", new DateOnly(2027, 4, 4), new DateOnly(2027, 4, 6)),
                new TripDestinationDraft(null, "Naples", new DateOnly(2027, 4, 7), new DateOnly(2027, 4, 8))
            ],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8),
            CreatedAt),
        Trip.Create(
            "Reykjavík",
            [
                new TripDestinationDraft(null, "Reykjavík", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 2)),
                new TripDestinationDraft(null, "Vík", new DateOnly(2027, 4, 3), null)
            ],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 3),
            CreatedAt)
    ];
}
