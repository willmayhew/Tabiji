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
