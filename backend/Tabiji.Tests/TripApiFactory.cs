using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;
using Tabiji.Tests.Fakes;

namespace Tabiji.Tests;

/// <summary>
/// Hosts the real API with the trip repository replaced by an in-memory fake, so the
/// endpoint tests exercise routing, status codes and JSON without needing a database.
/// </summary>
internal sealed class TripApiFactory : TabijiWebApplicationFactory
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    internal FakeTripRepository Repository { get; }

    internal TripApiFactory()
        : this(DefaultSeed)
    {
    }

    internal TripApiFactory(IEnumerable<Trip> seed)
    {
        Repository = new FakeTripRepository(seed);
    }

    protected override void ConfigureServices(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITripRepository>();
            services.AddSingleton<ITripRepository>(Repository);
        });
    }

    /// <summary>
    /// The seed trips. They deliberately travel to more than one country, so the
    /// ordering rule and the destination rules are exercised against a set that
    /// does not assume every trip is in Japan.
    /// </summary>
    private static Trip[] DefaultSeed =>
    [
        Trip.Create("Kyoto", "Japan", new DateOnly(2027, 4, 10), new DateOnly(2027, 4, 14), CreatedAt),
        Trip.Create("Rome", "Italy", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 8), CreatedAt),
        Trip.Create("Reykjavík", "Iceland", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 3), CreatedAt)
    ];
}
