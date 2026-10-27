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
    public async Task AddAsync_WritesTheDetailsOfAStop()
    {
        var trip = Trip.Create(
            "Japan Adventure",
            [
                new TripDestinationDraft(
                    null,
                    "Kyoto",
                    new DateOnly(2027, 5, 16),
                    new DateOnly(2027, 5, 20),
                    null,
                    null,
                    [
                        new TripStopDraft(
                            null,
                            "Gion",
                            null,
                            null,
                            35.0037,
                            135.7788,
                            PlaceCategory.Sight,
                            "Gion, Kyoto",
                            "Go early.",
                            new DateOnly(2027, 5, 17)),
                    ]),
            ],
            StartsOn,
            EndsOn,
            CreatedAt);

        await _repository!.AddAsync(trip);

        var reloaded = await ReloadAsync(trip);
        var stop = Assert.Single(reloaded.StopsOf(reloaded.ParentDestinations[0].Id));

        Assert.Equal(TripPlaceKind.Stop, stop.Kind);
        Assert.Equal(PlaceCategory.Sight, stop.Category);
        Assert.Equal("Gion, Kyoto", stop.Address);
        Assert.Equal("Go early.", stop.Notes);
        Assert.Equal(new DateOnly(2027, 5, 17), stop.PlannedOn);
        Assert.Equal(35.0037, stop.Latitude);
        Assert.Equal(135.7788, stop.Longitude);
    }

    [DatabaseFact]
    public async Task UpdateAsync_FilesALegacyPlaceUnderADestination()
    {
        var trip = Trip.Create(
            "Japan Adventure",
            [new TripDestinationDraft(null, "Kyoto", new DateOnly(2027, 5, 16), new DateOnly(2027, 5, 20))],
            StartsOn,
            EndsOn,
            CreatedAt);

        await _repository!.AddAsync(trip);

        var placeId = Guid.NewGuid();
        var plannedOn = new DateOnly(2027, 5, 18);

        await _context!.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO [TripDestinations]
                 ([Id], [TripId], [ParentDestinationId], [Kind], [Name], [SortOrder],
                  [StartsOn], [EndsOn], [Latitude], [Longitude], [Category], [Address],
                  [Notes], [PlannedOn], [CreatedAt], [MigratedFromSavedPlace])
             VALUES
                 ({placeId}, {trip.Id}, NULL, 'Place', 'Someday', 0,
                  NULL, NULL, 35.0, 135.0, 'Sight', 'Somewhere',
                  'One day.', {plannedOn}, SYSDATETIMEOFFSET(), 1)
             """);

        var reloaded = await ReloadAsync(trip);
        var parent = reloaded.ParentDestinations[0];
        var place = Assert.Single(reloaded.UnfiledPlaces);

        reloaded.ApplyRoute(
            reloaded.Name,
            reloaded.StartsOn,
            reloaded.EndsOn,
            [
                new TripDestinationDraft(
                    parent.Id,
                    parent.Name,
                    parent.StartsOn,
                    parent.EndsOn,
                    null,
                    null,
                    [
                        new TripStopDraft(
                            place.Id,
                            place.Name,
                            null,
                            null,
                            place.Latitude,
                            place.Longitude,
                            place.Category,
                            place.Address,
                            place.Notes,
                            place.PlannedOn),
                    ]),
            ],
            []);

        await _repository.UpdateAsync(reloaded);

        var after = await ReloadAsync(reloaded);
        var filed = Assert.Single(after.StopsOf(parent.Id));

        Assert.Equal(placeId, filed.Id);
        Assert.Equal(TripPlaceKind.Stop, filed.Kind);
        Assert.Equal(parent.Id, filed.ParentDestinationId);
        Assert.Empty(after.UnfiledPlaces);
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

    private static Trip JapanWithStops() => Trip.Create(
        "Japan",
        [
            new TripDestinationDraft(
                null,
                "Osaka",
                new DateOnly(2027, 5, 16),
                new DateOnly(2027, 5, 20),
                null,
                null,
                [TripStopDraft.Undated("Namba"), TripStopDraft.Undated("Umeda")]),
            new TripDestinationDraft(
                null,
                "Kyoto",
                new DateOnly(2027, 5, 20),
                new DateOnly(2027, 5, 24),
                null,
                null,
                [TripStopDraft.Undated("Gion")]),
        ],
        StartsOn,
        EndsOn,
        CreatedAt);

    [DatabaseFact]
    public async Task AddAsync_WritesTheNestedHierarchyInOrder()
    {
        var trip = JapanWithStops();

        await _repository!.AddAsync(trip);

        var reloaded = await ReloadAsync(trip);

        Assert.Equal(["Osaka", "Kyoto"], reloaded.ParentDestinations.Select(d => d.Name));
        Assert.Equal([0, 1], reloaded.ParentDestinations.Select(d => d.SortOrder));

        var osaka = reloaded.ParentDestinations[0];
        var kyoto = reloaded.ParentDestinations[1];

        Assert.Equal(["Namba", "Umeda"], reloaded.StopsOf(osaka.Id).Select(s => s.Name));
        Assert.Equal([0, 1], reloaded.StopsOf(osaka.Id).Select(s => s.SortOrder));
        Assert.Equal(["Gion"], reloaded.StopsOf(kyoto.Id).Select(s => s.Name));
    }

    [DatabaseFact]
    public async Task UpdateAsync_AddsAStopUnderAnExistingDestination()
    {
        var trip = JapanWithStops();

        await _repository!.AddAsync(trip);

        var reloaded = await ReloadAsync(trip);
        var osaka = reloaded.ParentDestinations[0];
        var kyoto = reloaded.ParentDestinations[1];

        reloaded.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                Draft(osaka, [.. Drafts(reloaded.StopsOf(osaka.Id)), NewStop("Tennoji")]),
                Draft(kyoto, Drafts(reloaded.StopsOf(kyoto.Id))),
            ]);

        await _repository.UpdateAsync(reloaded);

        var again = await ReloadAsync(reloaded);

        Assert.Equal(["Namba", "Umeda", "Tennoji"], again.StopsOf(osaka.Id).Select(s => s.Name));
    }

    [DatabaseFact]
    public async Task UpdateAsync_ReordersStopsWithinOneDestination()
    {
        var trip = JapanWithStops();

        await _repository!.AddAsync(trip);

        var reloaded = await ReloadAsync(trip);
        var osaka = reloaded.ParentDestinations[0];
        var kyoto = reloaded.ParentDestinations[1];
        var osakaStops = reloaded.StopsOf(osaka.Id);

        reloaded.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                Draft(osaka, [Draft(osakaStops[1]), Draft(osakaStops[0])]),
                Draft(kyoto, Drafts(reloaded.StopsOf(kyoto.Id))),
            ]);

        await _repository.UpdateAsync(reloaded);

        var again = await ReloadAsync(reloaded);

        Assert.Equal(["Umeda", "Namba"], again.StopsOf(osaka.Id).Select(s => s.Name));
        Assert.Equal([0, 1], again.StopsOf(osaka.Id).Select(s => s.SortOrder));
        Assert.Equal(["Osaka", "Kyoto"], again.ParentDestinations.Select(d => d.Name));
    }

    [DatabaseFact]
    public async Task UpdateAsync_DeletesAStopAndKeepsTheRest()
    {
        var trip = JapanWithStops();

        await _repository!.AddAsync(trip);

        var reloaded = await ReloadAsync(trip);
        var osaka = reloaded.ParentDestinations[0];
        var kyoto = reloaded.ParentDestinations[1];
        var osakaStops = reloaded.StopsOf(osaka.Id);

        reloaded.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                Draft(osaka, [Draft(osakaStops[1])]),
                Draft(kyoto, Drafts(reloaded.StopsOf(kyoto.Id))),
            ]);

        await _repository.UpdateAsync(reloaded);

        var again = await ReloadAsync(reloaded);

        Assert.Equal(["Umeda"], again.StopsOf(osaka.Id).Select(s => s.Name));
        Assert.Equal(["Gion"], again.StopsOf(kyoto.Id).Select(s => s.Name));
    }

    [DatabaseFact]
    public async Task UpdateAsync_RemovesADestinationAndItsStops()
    {
        var trip = JapanWithStops();

        await _repository!.AddAsync(trip);

        var reloaded = await ReloadAsync(trip);
        var osaka = reloaded.ParentDestinations[0];
        var kyoto = reloaded.ParentDestinations[1];

        reloaded.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [Draft(kyoto, Drafts(reloaded.StopsOf(kyoto.Id)))]);

        await _repository.UpdateAsync(reloaded);

        var again = await ReloadAsync(reloaded);

        Assert.Equal(["Kyoto"], again.ParentDestinations.Select(d => d.Name));
        Assert.Empty(again.StopsOf(osaka.Id));
        Assert.Equal(["Gion"], again.StopsOf(kyoto.Id).Select(s => s.Name));
    }

    [DatabaseFact]
    public async Task UpdateAsync_SwapsStopsBetweenTwoDestinations()
    {
        var trip = JapanWithStops();

        await _repository!.AddAsync(trip);

        var reloaded = await ReloadAsync(trip);
        var osaka = reloaded.ParentDestinations[0];
        var kyoto = reloaded.ParentDestinations[1];
        var osakaStops = reloaded.StopsOf(osaka.Id);
        var kyotoStops = reloaded.StopsOf(kyoto.Id);

        // Osaka receives Gion; Kyoto receives Namba and Umeda. Both stops
        // change parent in the same transaction, which is the case the
        // repository's parent-first, position-second write is for.
        reloaded.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                Draft(osaka, [Draft(kyotoStops[0])]),
                Draft(kyoto, [Draft(osakaStops[0]), Draft(osakaStops[1])]),
            ]);

        await _repository.UpdateAsync(reloaded);

        var again = await ReloadAsync(reloaded);

        Assert.Equal(["Gion"], again.StopsOf(osaka.Id).Select(s => s.Name));
        Assert.Equal(["Namba", "Umeda"], again.StopsOf(kyoto.Id).Select(s => s.Name));
        Assert.Equal(kyotoStops[0].Id, again.StopsOf(osaka.Id)[0].Id);
        Assert.Equal(osakaStops[0].Id, again.StopsOf(kyoto.Id)[0].Id);
    }

    private static TripDestinationDraft Draft(
        TripDestination parent,
        IReadOnlyList<TripStopDraft> stops) =>
        new(
            parent.Id,
            parent.Name,
            parent.StartsOn,
            parent.EndsOn,
            parent.Latitude,
            parent.Longitude,
            stops);

    private static TripStopDraft[] Drafts(IReadOnlyList<TripDestination> stops) =>
        stops.Select(Draft).ToArray();

    private static TripStopDraft Draft(TripDestination stop) =>
        new(stop.Id, stop.Name, stop.StartsOn, stop.EndsOn, stop.Latitude, stop.Longitude);

    private static TripStopDraft NewStop(string name) =>
        new(null, name, null, null);
}
