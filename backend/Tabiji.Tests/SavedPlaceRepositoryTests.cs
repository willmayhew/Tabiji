using Microsoft.EntityFrameworkCore;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence;
using Tabiji.Infrastructure.Persistence.Repositories;

namespace Tabiji.Tests;

/// <summary>
/// The saved-place repository, against a real database.
///
/// Everything about a place that can be decided in code is decided without a
/// database. What is left is the behaviour of the schema itself, which is the
/// one thing a fake cannot show: that a place goes with its trip, and that a
/// place outlives the stop it was filed under.
///
/// It has a database of its own, created with the real migrations and dropped
/// again afterwards.
/// </summary>
public sealed class SavedPlaceRepositoryTests : IAsyncLifetime, IDisposable
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiSavedPlaceRepositoryTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private TabijiDbContext? _context;
    private TripRepository? _trips;
    private SavedPlaceRepository? _places;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        _context = new TabijiDbContext(options);

        // The real migrations, so that the schema under test is the schema
        // that ships.
        await _context.Database.MigrateAsync();

        _trips = new TripRepository(_context);
        _places = new SavedPlaceRepository(_context);
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.Database.EnsureDeletedAsync();
        }

        Dispose();
    }

    public void Dispose()
    {
        _context?.Dispose();

        GC.SuppressFinalize(this);
    }

    private static Trip Kyoto() => Trip.Create(
        "Kyoto",
        [TripDestinationDraft.Undated("Kyoto"), TripDestinationDraft.Undated("Nara")],
        new DateOnly(2027, 4, 10),
        new DateOnly(2027, 4, 14),
        CreatedAt);

    private static SavedPlace Place(
        Guid tripId,
        string name,
        DateTimeOffset createdAt,
        Guid? destinationId = null,
        PlaceCategory? category = PlaceCategory.Sight)
    {
        var place = SavedPlace.Create(
            tripId,
            name,
            34.9671,
            135.7727,
            category,
            "Kyoto, Japan",
            "A note.",
            createdAt);

        place.MoveToDestination(destinationId);

        return place;
    }

    /// <summary>
    /// Reads the place back from the database rather than from the change
    /// tracker, so that what is asserted is what was written.
    /// </summary>
    private async Task<SavedPlace> ReloadAsync(Guid id)
    {
        _context!.ChangeTracker.Clear();

        var reloaded = await _places!.GetByIdAsync(id);

        Assert.NotNull(reloaded);

        return reloaded!;
    }

    [DatabaseFact]
    public async Task AddAsync_WritesThePlace()
    {
        var trip = Kyoto();

        await _trips!.AddAsync(trip);

        var place = Place(trip.Id, "Fushimi Inari", CreatedAt, trip.Destinations[0].Id);

        await _places!.AddAsync(place);

        var reloaded = await ReloadAsync(place.Id);

        Assert.Equal(trip.Id, reloaded.TripId);
        Assert.Equal("Fushimi Inari", reloaded.Name);
        Assert.Equal(34.9671, reloaded.Latitude);
        Assert.Equal(135.7727, reloaded.Longitude);
        Assert.Equal(PlaceCategory.Sight, reloaded.Category);
        Assert.Equal("Kyoto, Japan", reloaded.Address);
        Assert.Equal("A note.", reloaded.Notes);
        Assert.Equal(trip.Destinations[0].Id, reloaded.DestinationId);
        Assert.Equal(CreatedAt, reloaded.CreatedAt);
    }

    [DatabaseFact]
    public async Task AddAsync_WritesAPlaceWithNoCategoryAndNoFiling()
    {
        var trip = Kyoto();

        await _trips!.AddAsync(trip);

        var place = SavedPlace.Create(
            trip.Id,
            "Somewhere",
            0.0,
            0.0,
            null,
            null,
            null,
            CreatedAt);

        await _places!.AddAsync(place);

        var reloaded = await ReloadAsync(place.Id);

        Assert.Null(reloaded.Category);
        Assert.Null(reloaded.Address);
        Assert.Null(reloaded.Notes);
        Assert.Null(reloaded.DestinationId);
    }

    [DatabaseFact]
    public async Task ListForTripAsync_OrdersByTheTimeSavedAndThenByName()
    {
        var kyoto = Kyoto();
        var rome = Trip.Create(
            "Rome",
            [TripDestinationDraft.Undated("Rome")],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8),
            CreatedAt);

        await _trips!.AddAsync(kyoto);
        await _trips.AddAsync(rome);

        await _places!.AddAsync(Place(kyoto.Id, "Nishiki Market", CreatedAt.AddHours(2)));
        await _places.AddAsync(Place(kyoto.Id, "Fushimi Inari", CreatedAt));
        await _places.AddAsync(Place(rome.Id, "Colosseum", CreatedAt));

        var listed = await _places.ListForTripAsync(kyoto.Id);

        // Another trip's places are never listed, and the order is the one the
        // repository promises rather than whatever the server felt like.
        Assert.Equal(["Fushimi Inari", "Nishiki Market"], listed.Select(place => place.Name));
    }

    [DatabaseFact]
    public async Task ListForTripAsync_ReturnsNothingForATripWithNoPlaces()
    {
        var trip = Kyoto();

        await _trips!.AddAsync(trip);

        Assert.Empty(await _places!.ListForTripAsync(trip.Id));
    }

    [DatabaseFact]
    public async Task UpdateAsync_WritesTheChangedFieldsAndTheFiling()
    {
        var trip = Kyoto();

        await _trips!.AddAsync(trip);

        var place = Place(trip.Id, "Fushimi Inari", CreatedAt);

        await _places!.AddAsync(place);

        var stored = await ReloadAsync(place.Id);

        stored.Edit("Nishiki Market", 35.0050, 135.7649, PlaceCategory.Food, "Kyoto", null);
        stored.MoveToDestination(trip.Destinations[1].Id);

        await _places.UpdateAsync(stored);

        var reloaded = await ReloadAsync(place.Id);

        Assert.Equal("Nishiki Market", reloaded.Name);
        Assert.Equal(35.0050, reloaded.Latitude);
        Assert.Equal(135.7649, reloaded.Longitude);
        Assert.Equal(PlaceCategory.Food, reloaded.Category);
        Assert.Equal("Kyoto", reloaded.Address);
        Assert.Null(reloaded.Notes);
        Assert.Equal(trip.Destinations[1].Id, reloaded.DestinationId);
        Assert.Equal(CreatedAt, reloaded.CreatedAt);
    }

    [DatabaseFact]
    public async Task UpdateAsync_TakesThePlaceOffTheRoute()
    {
        var trip = Kyoto();

        await _trips!.AddAsync(trip);

        var place = Place(trip.Id, "Fushimi Inari", CreatedAt, trip.Destinations[0].Id);

        await _places!.AddAsync(place);

        var stored = await ReloadAsync(place.Id);
        stored.MoveToDestination(null);

        await _places.UpdateAsync(stored);

        Assert.Null((await ReloadAsync(place.Id)).DestinationId);
    }

    [DatabaseFact]
    public async Task RemoveAsync_DeletesThePlace()
    {
        var trip = Kyoto();

        await _trips!.AddAsync(trip);

        var place = Place(trip.Id, "Fushimi Inari", CreatedAt);

        await _places!.AddAsync(place);

        await _places.RemoveAsync(await ReloadAsync(place.Id));

        _context!.ChangeTracker.Clear();

        Assert.Null(await _places.GetByIdAsync(place.Id));
    }

    [DatabaseFact]
    public async Task WhenTheStopGoes_ThePlaceStaysAndComesOffTheRoute()
    {
        var trip = Kyoto();

        await _trips!.AddAsync(trip);

        var stop = trip.Destinations[0].Id;
        var kept = Place(trip.Id, "Fushimi Inari", CreatedAt, stop);

        await _places!.AddAsync(kept);

        // Removing the stop is the trip's own business, and is what the trip
        // endpoint does when a route is written back without it.
        _context!.ChangeTracker.Clear();

        var stored = await _trips.GetByIdAsync(trip.Id);
        Assert.NotNull(stored);

        stored!.RemoveDestination(stop);
        await _trips.UpdateAsync(stored);

        var reloaded = await ReloadAsync(kept.Id);

        // The place is still on the trip: nothing the reader saved is lost
        // with the stop it happened to be filed under.
        Assert.Equal(trip.Id, reloaded.TripId);
        Assert.Null(reloaded.DestinationId);
        Assert.Equal("Fushimi Inari", reloaded.Name);
        Assert.Single(await _places.ListForTripAsync(trip.Id));
    }

    [DatabaseFact]
    public async Task WhenTheTripGoes_ItsPlacesGoWithIt()
    {
        var trip = Kyoto();
        var other = Trip.Create(
            "Rome",
            [TripDestinationDraft.Undated("Rome")],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8),
            CreatedAt);

        await _trips!.AddAsync(trip);
        await _trips.AddAsync(other);

        var doomed = Place(trip.Id, "Fushimi Inari", CreatedAt, trip.Destinations[0].Id);
        var safe = Place(other.Id, "Colosseum", CreatedAt);

        await _places!.AddAsync(doomed);
        await _places.AddAsync(safe);

        _context!.ChangeTracker.Clear();

        var stored = await _context.Trips.SingleAsync(candidate => candidate.Id == trip.Id);

        // The database cascades to the trip's stops and to its places; nothing
        // has to be removed by hand, and nothing of another trip is touched.
        _context.Trips.Remove(stored);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        Assert.Null(await _places.GetByIdAsync(doomed.Id));
        Assert.NotNull(await _places.GetByIdAsync(safe.Id));
    }
}
