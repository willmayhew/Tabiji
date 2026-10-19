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

    private static Trip[] DefaultSeed =>
    [
        Trip.Create("Kyoto", "Kansai", new DateOnly(2027, 4, 10), new DateOnly(2027, 4, 14), CreatedAt),
        Trip.Create("Tokyo", "Kanto", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 8), CreatedAt),
        Trip.Create("Hakone", "Kanto", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 3), CreatedAt)
    ];
}
