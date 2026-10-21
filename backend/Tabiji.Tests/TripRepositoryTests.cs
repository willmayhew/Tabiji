using Microsoft.EntityFrameworkCore;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence;
using Tabiji.Infrastructure.Persistence.Repositories;

namespace Tabiji.Tests;

/// <summary>
/// The trip repository, against a real database.
///
/// The rest of the suite runs without one, and the rules of a trip are all
/// checked without one. This file is about the one thing that cannot be
/// checked any other way: that the route is written to the database in the
/// order it was given, with its stops keeping their identity, and that a stop
/// taking the place of another does not collide with the unique index on
/// (TripId, SortOrder).
///
/// It has a database of its own, which it creates with the real migrations
/// and drops again afterwards.
/// </summary>
public sealed class TripRepositoryTests : IAsyncLifetime, IDisposable
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiRepositoryTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly DateOnly StartsOn = new(2027, 5, 16);
    private static readonly DateOnly EndsOn = new(2027, 6, 5);
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private TabijiDbContext? _context;
    private TripRepository? _repository;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        _context = new TabijiDbContext(options);

        // The real migrations, so that the schema under test is the schema
        // that ships.
        await _context.Database.MigrateAsync();

        _repository = new TripRepository(_context);
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

    private static Trip Japan() => Trip.Create(
        "Japan Adventure",
        [
            new TripDestinationDraft(null, "Osaka", new DateOnly(2027, 5, 16), new DateOnly(2027, 5, 20)),
            new TripDestinationDraft(null, "Kyoto", new DateOnly(2027, 5, 20), new DateOnly(2027, 5, 24)),
            new TripDestinationDraft(null, "Ise", null, null),
            new TripDestinationDraft(null, "Tokyo", new DateOnly(2027, 5, 26), new DateOnly(2027, 6, 5))
        ],
        StartsOn,
        EndsOn,
        CreatedAt);

    /// <summary>
    /// Reads the trip back from the database rather than from the change
    /// tracker, so that what is asserted is what was written.
    /// </summary>
    private async Task<Trip> ReloadAsync(Trip trip)
    {
        _context!.ChangeTracker.Clear();

        var reloaded = await _repository!.GetByIdAsync(trip.Id);

        Assert.NotNull(reloaded);

        return reloaded!;
    }

    [DatabaseFact]
    public async Task AddAsync_WritesTheRouteInOrder()
    {
        var trip = Japan();

        await _repository!.AddAsync(trip);

        var reloaded = await ReloadAsync(trip);

        Assert.Equal(["Osaka", "Kyoto", "Ise", "Tokyo"], reloaded.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2, 3], reloaded.Destinations.Select(d => d.SortOrder));
        Assert.Equal(new DateOnly(2027, 5, 16), reloaded.Destinations[0].StartsOn);
        Assert.Equal(new DateOnly(2027, 6, 5), reloaded.Destinations[3].EndsOn);
    }

    [DatabaseFact]
    public async Task UpdateAsync_ReversesTheRouteAndKeepsTheIdentityOfTheStops()
    {
        var trip = Japan();

        await _repository!.AddAsync(trip);

        var osaka = trip.Destinations[0].Id;
        var tokyo = trip.Destinations[3].Id;

        trip.ApplyRoute(
            "Japan Adventure",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(tokyo, "Tokyo", new DateOnly(2027, 5, 16), new DateOnly(2027, 5, 20)),
                new TripDestinationDraft(trip.Destinations[2].Id, "Ise", new DateOnly(2027, 5, 20), new DateOnly(2027, 5, 24)),
                new TripDestinationDraft(trip.Destinations[1].Id, "Kyoto", new DateOnly(2027, 5, 24), new DateOnly(2027, 5, 26)),
                new TripDestinationDraft(osaka, "Osaka", new DateOnly(2027, 5, 26), new DateOnly(2027, 6, 5))
            ]);

        await _repository!.UpdateAsync(trip);

        var reloaded = await ReloadAsync(trip);

        Assert.Equal(["Tokyo", "Ise", "Kyoto", "Osaka"], reloaded.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2, 3], reloaded.Destinations.Select(d => d.SortOrder));
        Assert.Equal(tokyo, reloaded.Destinations[0].Id);
        Assert.Equal(osaka, reloaded.Destinations[3].Id);

        // The dates went with the stops, rather than staying where they were.
        Assert.Equal(new DateOnly(2027, 5, 16), reloaded.Destinations[0].StartsOn);
        Assert.Equal(new DateOnly(2027, 6, 5), reloaded.Destinations[3].EndsOn);
    }

    [DatabaseFact]
    public async Task UpdateAsync_WritesTheNamesAndTheDatesOfTheStopsThatStay()
    {
        var trip = Japan();

        await _repository!.AddAsync(trip);

        var osaka = trip.Destinations[0].Id;

        trip.ApplyRoute(
            "Japan Adventure",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(osaka, "Osaka", new DateOnly(2027, 5, 17), new DateOnly(2027, 5, 19))
            ]);

        await _repository!.UpdateAsync(trip);

        var reloaded = await ReloadAsync(trip);

        var stop = Assert.Single(reloaded.Destinations);

        Assert.Equal("Osaka", stop.Name);
        Assert.Equal(new DateOnly(2027, 5, 17), stop.StartsOn);
        Assert.Equal(new DateOnly(2027, 5, 19), stop.EndsOn);
    }

    [DatabaseFact]
    public async Task UpdateAsync_ClearsADate()
    {
        var trip = Japan();

        await _repository!.AddAsync(trip);

        var osaka = trip.Destinations[0].Id;

        trip.ApplyRoute(
            "Japan Adventure",
            StartsOn,
            EndsOn,
            [new TripDestinationDraft(osaka, "Osaka", null, null)]);

        await _repository!.UpdateAsync(trip);

        var reloaded = await ReloadAsync(trip);

        var stop = Assert.Single(reloaded.Destinations);

        Assert.Null(stop.StartsOn);
        Assert.Null(stop.EndsOn);
    }

    [DatabaseFact]
    public async Task UpdateAsync_AddsAndRemovesStops()
    {
        var trip = Japan();

        await _repository!.AddAsync(trip);

        var osaka = trip.Destinations[0].Id;

        trip.ApplyRoute(
            "Japan Adventure",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(osaka, "Osaka", new DateOnly(2027, 5, 16), new DateOnly(2027, 5, 20)),
                new TripDestinationDraft(null, "Nagoya", null, null)
            ]);

        await _repository!.UpdateAsync(trip);

        var reloaded = await ReloadAsync(trip);

        Assert.Equal(["Osaka", "Nagoya"], reloaded.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1], reloaded.Destinations.Select(d => d.SortOrder));
        Assert.Equal(osaka, reloaded.Destinations[0].Id);
    }

    [DatabaseFact]
    public async Task UpdateAsync_RenamesTheTrip()
    {
        var trip = Japan();

        await _repository!.AddAsync(trip);

        trip.ApplyRoute(
            "日本",
            StartsOn,
            EndsOn,
            [new TripDestinationDraft(trip.Destinations[0].Id, "大阪", null, null)]);

        await _repository!.UpdateAsync(trip);

        var reloaded = await ReloadAsync(trip);

        Assert.Equal("日本", reloaded.Name);
        Assert.Equal("大阪", Assert.Single(reloaded.Destinations).Name);
    }
}
