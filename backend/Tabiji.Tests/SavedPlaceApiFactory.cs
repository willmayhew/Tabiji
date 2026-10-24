using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;
using Tabiji.Tests.Fakes;

namespace Tabiji.Tests;

/// <summary>
/// Hosts the real API with both repositories replaced by in-memory fakes, so
/// the saved-place endpoints exercise routing, status codes and JSON without
/// needing a database.
///
/// Two trips are seeded, each with its own stops, so that a place can be
/// offered a stop belonging to the <em>other</em> trip — the case a foreign
/// key cannot catch on its own.
/// </summary>
internal sealed class SavedPlaceApiFactory : TabijiWebApplicationFactory
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The trip the seeded places belong to.</summary>
    internal static readonly Trip Kyoto = Trip.Create(
        "Kyoto",
        [TripDestinationDraft.Undated("Kyoto"), TripDestinationDraft.Undated("Nara")],
        new DateOnly(2027, 4, 10),
        new DateOnly(2027, 4, 14),
        CreatedAt);

    /// <summary>A second trip, whose stops must never be available to the first.</summary>
    internal static readonly Trip Rome = Trip.Create(
        "Rome",
        [TripDestinationDraft.Undated("Rome"), TripDestinationDraft.Undated("Amalfi")],
        new DateOnly(2027, 4, 1),
        new DateOnly(2027, 4, 8),
        CreatedAt);

    internal FakeTripRepository Trips { get; }

    internal FakeSavedPlaceRepository Places { get; }

    internal SavedPlaceApiFactory()
        : this([Kyoto, Rome], DefaultPlaces())
    {
    }

    internal SavedPlaceApiFactory(IEnumerable<Trip> trips, IEnumerable<SavedPlace> places)
    {
        Trips = new FakeTripRepository(trips);
        Places = new FakeSavedPlaceRepository(places);
    }

    protected override void ConfigureServices(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITripRepository>();
            services.AddSingleton<ITripRepository>(Trips);

            services.RemoveAll<ISavedPlaceRepository>();
            services.AddSingleton<ISavedPlaceRepository>(Places);
        });
    }

    /// <summary>
    /// The places of the Kyoto trip. They carry different times so that the
    /// order they are listed in is settled rather than a race, and one of
    /// them is filed under a stop while the other is not.
    /// </summary>
    internal static SavedPlace[] DefaultPlaces() =>
    [
        Place(
            Kyoto,
            "Nishiki Market",
            35.0050,
            135.7649,
            PlaceCategory.Food,
            CreatedAt),
        Place(
            Kyoto,
            "Fushimi Inari",
            34.9671,
            135.7727,
            PlaceCategory.Sight,
            CreatedAt.AddHours(1),
            Kyoto.Destinations[0].Id)
    ];

    private static SavedPlace Place(
        Trip trip,
        string name,
        double latitude,
        double longitude,
        PlaceCategory? category,
        DateTimeOffset createdAt,
        Guid? destinationId = null)
    {
        var place = SavedPlace.Create(
            trip.Id,
            name,
            latitude,
            longitude,
            category,
            null,
            null,
            createdAt);

        place.MoveToDestination(destinationId);

        return place;
    }
}
