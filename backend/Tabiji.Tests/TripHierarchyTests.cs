using Tabiji.Domain.Trips;

namespace Tabiji.Tests;

/// <summary>
/// The hierarchy of a trip: its destinations on the main route, and the stops
/// within them.
///
/// The rules are deliberately one level deep. A destination may hold stops; a
/// stop holds nothing. The main route order belongs to the destinations, and a
/// stop's order is its order within its own destination.
/// </summary>
public sealed class TripHierarchyTests
{
    private static readonly DateOnly StartsOn = new(2027, 5, 8);
    private static readonly DateOnly EndsOn = new(2027, 5, 29);
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static TripStopDraft Stop(
        string name,
        DateOnly? startsOn = null,
        DateOnly? endsOn = null) =>
        new(null, name, startsOn, endsOn);

    private static TripDestinationDraft Destination(
        string name,
        DateOnly? startsOn = null,
        DateOnly? endsOn = null,
        params TripStopDraft[] stops) =>
        new(null, name, startsOn, endsOn, null, null, stops);

    private static Trip Japan() => Trip.Create(
        "Japan",
        [
            Destination(
                "Osaka",
                new DateOnly(2027, 5, 8),
                new DateOnly(2027, 5, 12),
                Stop("Namba"),
                Stop("Umeda")),
            Destination(
                "Kyoto",
                new DateOnly(2027, 5, 13),
                new DateOnly(2027, 5, 17),
                Stop("Gion"),
                Stop("Arashiyama")),
            Destination(
                "Tokyo",
                new DateOnly(2027, 5, 20),
                new DateOnly(2027, 5, 25),
                Stop("Shibuya"),
                Stop("Akihabara")),
        ],
        StartsOn,
        EndsOn,
        CreatedAt);

    [Fact]
    public void Create_NestsEachStopUnderItsDestination()
    {
        var trip = Japan();

        Assert.Equal(["Osaka", "Kyoto", "Tokyo"], trip.ParentDestinations.Select(d => d.Name));
        Assert.Equal(["Namba", "Umeda"], trip.StopsOf(trip.ParentDestinations[0].Id).Select(s => s.Name));
        Assert.Equal(["Gion", "Arashiyama"], trip.StopsOf(trip.ParentDestinations[1].Id).Select(s => s.Name));
        Assert.Equal(["Shibuya", "Akihabara"], trip.StopsOf(trip.ParentDestinations[2].Id).Select(s => s.Name));
    }

    [Fact]
    public void Create_NumbersTheRouteAndEachDestinationsStopsSeparately()
    {
        var trip = Japan();

        Assert.Equal([0, 1, 2], trip.ParentDestinations.Select(d => d.SortOrder));

        foreach (var parent in trip.ParentDestinations)
        {
            Assert.Equal([0, 1], trip.StopsOf(parent.Id).Select(stop => stop.SortOrder));
        }
    }

    [Fact]
    public void Create_KeepsStopsOutOfTheMainRoute()
    {
        var trip = Japan();

        Assert.Equal(3, trip.ParentDestinations.Count);
        Assert.Equal(9, trip.Destinations.Count);
        Assert.All(trip.ParentDestinations, parent => Assert.Null(parent.ParentDestinationId));
        Assert.All(
            trip.Destinations.Where(destination => destination.ParentDestinationId is not null),
            stop => Assert.True(stop.IsStop));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankStopName(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => Trip.Create(
            "Japan",
            [Destination("Osaka", stops: Stop(name))],
            StartsOn,
            EndsOn,
            CreatedAt));

        Assert.Equal("destinations", exception.ParamName);
        Assert.Contains("stop name is required", exception.Message);
    }

    [Fact]
    public void Create_RejectsTooManyStopsInOneDestination()
    {
        var stops = Enumerable.Range(1, Trip.MaxStopsPerDestination + 1)
            .Select(index => Stop($"Stop {index}"))
            .ToArray();

        var exception = Assert.Throws<ArgumentException>(() => Trip.Create(
            "Japan",
            [Destination("Osaka", stops: stops)],
            StartsOn,
            EndsOn,
            CreatedAt));

        Assert.Equal("destinations", exception.ParamName);
        Assert.Contains("stops", exception.Message);
    }

    [Fact]
    public void Create_RejectsAStopNamedTwice()
    {
        var id = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() => Trip.Create(
            "Japan",
            [
                Destination("Osaka", stops: new TripStopDraft(id, "Namba", null, null)),
                Destination("Kyoto", stops: new TripStopDraft(id, "Gion", null, null)),
            ],
            StartsOn,
            EndsOn,
            CreatedAt));

        Assert.Equal("destinations", exception.ParamName);
        Assert.Contains("only once", exception.Message);
    }

    [Fact]
    public void Create_RejectsAStopOutsideItsDestination()
    {
        var exception = Assert.Throws<ArgumentException>(() => Trip.Create(
            "Japan",
            [
                Destination(
                    "Osaka",
                    new DateOnly(2027, 5, 8),
                    new DateOnly(2027, 5, 12),
                    Stop("Namba", new DateOnly(2027, 5, 13), new DateOnly(2027, 5, 14))),
            ],
            StartsOn,
            EndsOn,
            CreatedAt));

        Assert.Equal("destinations", exception.ParamName);
        Assert.Contains("\"Namba\", a stop at \"Osaka\"", exception.Message);
        Assert.Contains("after \"Osaka\" ends", exception.Message);
    }

    [Fact]
    public void Create_AllowsAStopWithNoDates()
    {
        var trip = Trip.Create(
            "Japan",
            [Destination("Osaka", stops: Stop("Namba"))],
            StartsOn,
            EndsOn,
            CreatedAt);

        var stop = Assert.Single(trip.StopsOf(trip.ParentDestinations[0].Id));

        Assert.Null(stop.StartsOn);
        Assert.Null(stop.EndsOn);
    }

    [Fact]
    public void ApplyRoute_AddsStopsAndKeepsTheIdentityOfTheExistingOnes()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var namba = trip.StopsOf(osaka.Id)[0];

        trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(
                    osaka.Id,
                    "Osaka",
                    osaka.StartsOn,
                    osaka.EndsOn,
                    null,
                    null,
                    [
                        new TripStopDraft(namba.Id, "Namba", null, null),
                        new TripStopDraft(null, "Tennoji", null, null),
                    ]),
                new TripDestinationDraft(
                    trip.ParentDestinations[1].Id,
                    "Kyoto",
                    trip.ParentDestinations[1].StartsOn,
                    trip.ParentDestinations[1].EndsOn,
                    null,
                    null,
                    trip.StopsOf(trip.ParentDestinations[1].Id)
                        .Select(stop => new TripStopDraft(stop.Id, stop.Name, null, null))
                        .ToArray()),
                new TripDestinationDraft(
                    trip.ParentDestinations[2].Id,
                    "Tokyo",
                    trip.ParentDestinations[2].StartsOn,
                    trip.ParentDestinations[2].EndsOn,
                    null,
                    null,
                    trip.StopsOf(trip.ParentDestinations[2].Id)
                        .Select(stop => new TripStopDraft(stop.Id, stop.Name, null, null))
                        .ToArray()),
            ]);

        var stops = trip.StopsOf(osaka.Id);

        Assert.Equal(["Namba", "Tennoji"], stops.Select(stop => stop.Name));
        Assert.Equal(namba.Id, stops[0].Id);
        Assert.NotEqual(Guid.Empty, stops[1].Id);
    }

    [Fact]
    public void ApplyRoute_RemovesAStopThatIsLeftOut()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];

        trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(
                    osaka.Id, "Osaka", osaka.StartsOn, osaka.EndsOn, null, null,
                    [new TripStopDraft(trip.StopsOf(osaka.Id)[1].Id, "Umeda", null, null)]),
                .. OtherParents(trip),
            ]);

        Assert.Equal(["Umeda"], trip.StopsOf(osaka.Id).Select(stop => stop.Name));
    }

    [Fact]
    public void ApplyRoute_ReordersStopsWithoutChangingTheMainRoute()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var stops = trip.StopsOf(osaka.Id);

        trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(
                    osaka.Id, "Osaka", osaka.StartsOn, osaka.EndsOn, null, null,
                    [
                        new TripStopDraft(stops[1].Id, stops[1].Name, null, null),
                        new TripStopDraft(stops[0].Id, stops[0].Name, null, null),
                    ]),
                .. OtherParents(trip),
            ]);

        Assert.Equal(["Umeda", "Namba"], trip.StopsOf(osaka.Id).Select(stop => stop.Name));
        Assert.Equal(["Osaka", "Kyoto", "Tokyo"], trip.ParentDestinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2], trip.ParentDestinations.Select(d => d.SortOrder));
    }

    [Fact]
    public void ApplyRoute_ReparentsAStopBetweenExistingDestinations()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var kyoto = trip.ParentDestinations[1];
        var namba = trip.StopsOf(osaka.Id)[0];
        var kyotoStops = trip.StopsOf(kyoto.Id);

        trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(
                    osaka.Id, "Osaka", osaka.StartsOn, osaka.EndsOn, null, null,
                    [new TripStopDraft(trip.StopsOf(osaka.Id)[1].Id, "Umeda", null, null)]),
                new TripDestinationDraft(
                    kyoto.Id, "Kyoto", kyoto.StartsOn, kyoto.EndsOn, null, null,
                    [
                        new TripStopDraft(kyotoStops[0].Id, "Gion", null, null),
                        new TripStopDraft(kyotoStops[1].Id, "Arashiyama", null, null),
                        new TripStopDraft(namba.Id, "Namba", null, null),
                    ]),
                .. OtherParents(trip).Skip(1),
            ]);

        Assert.Equal(["Umeda"], trip.StopsOf(osaka.Id).Select(stop => stop.Name));
        Assert.Equal(["Gion", "Arashiyama", "Namba"], trip.StopsOf(kyoto.Id).Select(stop => stop.Name));
        Assert.Equal(namba.Id, trip.StopsOf(kyoto.Id)[2].Id);
        Assert.Equal(kyoto.Id, trip.StopsOf(kyoto.Id)[2].ParentDestinationId);
    }

    [Fact]
    public void ApplyRoute_RejectsMovingAStopToANewDestination()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var namba = trip.StopsOf(osaka.Id)[0];

        var exception = Assert.Throws<ArgumentException>(() => trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(
                    osaka.Id, "Osaka", osaka.StartsOn, osaka.EndsOn, null, null,
                    [new TripStopDraft(trip.StopsOf(osaka.Id)[1].Id, "Umeda", null, null)]),
                .. OtherParents(trip),
                new TripDestinationDraft(
                    null, "Nagoya", null, null, null, null,
                    [new TripStopDraft(namba.Id, "Namba", null, null)]),
            ]));

        Assert.Equal("destinations", exception.ParamName);
        Assert.Contains("new destination", exception.Message);
    }

    [Fact]
    public void ApplyRoute_RejectsAnUnknownStopIdentity()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];

        var exception = Assert.Throws<ArgumentException>(() => trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(
                    osaka.Id, "Osaka", osaka.StartsOn, osaka.EndsOn, null, null,
                    [new TripStopDraft(Guid.NewGuid(), "Nowhere", null, null)]),
                .. OtherParents(trip),
            ]));

        Assert.Equal("destinations", exception.ParamName);
        Assert.Contains("does not have that stop", exception.Message);
    }

    [Fact]
    public void ApplyRoute_RemovesADestinationAndAllOfItsStops()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var kyoto = trip.ParentDestinations[1];

        trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [.. OtherParents(trip)]);

        Assert.Equal(["Kyoto", "Tokyo"], trip.ParentDestinations.Select(d => d.Name));
        Assert.Equal([0, 1], trip.ParentDestinations.Select(d => d.SortOrder));
        Assert.Empty(trip.StopsOf(osaka.Id));
        Assert.Equal(2, trip.StopsOf(kyoto.Id).Count);
    }

    [Fact]
    public void ApplyRoute_ChangesNothingWhenTheRouteIsRefused()
    {
        var trip = Japan();
        var before = Snapshot(trip);

        Assert.Throws<ArgumentException>(() => trip.ApplyRoute(
            "Changed",
            StartsOn,
            EndsOn,
            [Destination("Osaka", stops: Stop("Namba", StartsOn.AddDays(-1), StartsOn))]));

        Assert.Equal(before, Snapshot(trip));
    }

    [Fact]
    public void RemoveDestination_RemovesItsStopsAndClosesTheRoute()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];

        trip.RemoveDestination(osaka.Id);

        Assert.Equal(["Kyoto", "Tokyo"], trip.ParentDestinations.Select(d => d.Name));
        Assert.DoesNotContain(trip.Destinations, destination => destination.ParentDestinationId == osaka.Id);
    }

    [Fact]
    public void RemoveStop_ClosesTheGap()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var namba = trip.StopsOf(osaka.Id)[0];

        trip.RemoveStop(osaka.Id, namba.Id);

        var remaining = Assert.Single(trip.StopsOf(osaka.Id));

        Assert.Equal("Umeda", remaining.Name);
        Assert.Equal(0, remaining.SortOrder);
    }

    [Fact]
    public void ReorderDestinations_KeepsEveryStopWithItsDestination()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var kyoto = trip.ParentDestinations[1];
        var tokyo = trip.ParentDestinations[2];

        trip.ReorderDestinations([tokyo.Id, osaka.Id, kyoto.Id]);

        Assert.Equal(["Tokyo", "Osaka", "Kyoto"], trip.ParentDestinations.Select(d => d.Name));
        Assert.Equal(["Shibuya", "Akihabara"], trip.StopsOf(tokyo.Id).Select(s => s.Name));
        Assert.Equal(["Namba", "Umeda"], trip.StopsOf(osaka.Id).Select(s => s.Name));
        Assert.Equal([0, 1, 2], trip.ParentDestinations.Select(d => d.SortOrder));
    }

    [Fact]
    public void ReorderStops_PutsTheStopsInTheNewOrder()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var namba = trip.StopsOf(osaka.Id)[0];
        var umeda = trip.StopsOf(osaka.Id)[1];

        trip.ReorderStops(osaka.Id, [umeda.Id, namba.Id]);

        var stops = trip.StopsOf(osaka.Id);

        Assert.Equal(["Umeda", "Namba"], stops.Select(stop => stop.Name));
        Assert.Equal([0, 1], stops.Select(stop => stop.SortOrder));

        // The other destinations are untouched.
        Assert.Equal(
            ["Gion", "Arashiyama"],
            trip.StopsOf(trip.ParentDestinations[1].Id).Select(stop => stop.Name));
    }

    [Fact]
    public void Create_KeepsTheDetailsOfAStop()
    {
        var parentStarts = StartsOn;
        var parentEnds = StartsOn.AddDays(3);
        var plannedOn = StartsOn.AddDays(1);

        var trip = Trip.Create(
            "Japan",
            [
                new TripDestinationDraft(
                    null,
                    "Kyoto",
                    parentStarts,
                    parentEnds,
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
                            plannedOn),
                    ]),
            ],
            StartsOn,
            EndsOn,
            CreatedAt);

        var stop = Assert.Single(trip.StopsOf(trip.ParentDestinations[0].Id));

        Assert.Equal(TripPlaceKind.Stop, stop.Kind);
        Assert.Equal(PlaceCategory.Sight, stop.Category);
        Assert.Equal("Gion, Kyoto", stop.Address);
        Assert.Equal("Go early.", stop.Notes);
        Assert.Equal(plannedOn, stop.PlannedOn);
        Assert.Equal(35.0037, stop.Latitude);
        Assert.Equal(135.7788, stop.Longitude);
    }

    [Fact]
    public void Create_RejectsAPlannedDayOutsideTheTrip()
    {
        var exception = Assert.Throws<ArgumentException>(() => Trip.Create(
            "Japan",
            [
                new TripDestinationDraft(
                    null,
                    "Kyoto",
                    StartsOn,
                    StartsOn.AddDays(3),
                    null,
                    null,
                    [
                        new TripStopDraft(
                            null,
                            "Gion",
                            null,
                            null,
                            null,
                            null,
                            PlaceCategory.Sight,
                            null,
                            null,
                            EndsOn.AddDays(1)),
                    ]),
            ],
            StartsOn,
            EndsOn,
            CreatedAt));

        Assert.Contains("Gion", exception.Message);
        Assert.Contains("after the trip ends", exception.Message);
    }

    [Fact]
    public void ApplyRoute_ChangesTheDetailsAndKeepsTheStopIdentity()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];
        var namba = trip.StopsOf(osaka.Id)[0];

        trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(
                    osaka.Id,
                    "Osaka",
                    osaka.StartsOn,
                    osaka.EndsOn,
                    null,
                    null,
                    [
                        new TripStopDraft(
                            namba.Id,
                            "Namba at night",
                            null,
                            null,
                            34.666,
                            135.5,
                            PlaceCategory.Food,
                            "Namba, Osaka",
                            "Eat here.",
                            StartsOn.AddDays(2)),
                        new TripStopDraft(trip.StopsOf(osaka.Id)[1].Id, "Umeda", null, null),
                    ]),
                .. OtherParents(trip),
            ]);

        var edited = trip.StopsOf(osaka.Id)[0];

        Assert.Equal(namba.Id, edited.Id);
        Assert.Equal("Namba at night", edited.Name);
        Assert.Equal(PlaceCategory.Food, edited.Category);
        Assert.Equal("Namba, Osaka", edited.Address);
        Assert.Equal("Eat here.", edited.Notes);
        Assert.Equal(StartsOn.AddDays(2), edited.PlannedOn);
    }

    [Fact]
    public void AddStop_KeepsItsDetails()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];

        trip.AddStop(
            osaka.Id,
            "Tennoji",
            category: PlaceCategory.Shop,
            address: "Tennoji, Osaka",
            notes: "Market",
            plannedOn: StartsOn.AddDays(1));

        var stop = trip.StopsOf(osaka.Id)[^1];

        Assert.Equal(TripPlaceKind.Stop, stop.Kind);
        Assert.Equal(PlaceCategory.Shop, stop.Category);
        Assert.Equal("Tennoji, Osaka", stop.Address);
        Assert.Equal("Market", stop.Notes);
        Assert.Equal(StartsOn.AddDays(1), stop.PlannedOn);
    }

    [Fact]
    public void AddStop_AppendsAndChecksTheDateBounds()
    {
        var trip = Japan();
        var osaka = trip.ParentDestinations[0];

        trip.AddStop(osaka.Id, "Tennoji");

        Assert.Equal("Tennoji", trip.StopsOf(osaka.Id)[^1].Name);
        Assert.Equal(2, trip.StopsOf(osaka.Id)[^1].SortOrder);

        var exception = Assert.Throws<ArgumentException>(() =>
            trip.AddStop(osaka.Id, "Outside", new DateOnly(2027, 5, 13), new DateOnly(2027, 5, 14)));

        Assert.Contains("after \"Osaka\" ends", exception.Message);
    }

    [Fact]
    public void AddStop_RejectsAnUnknownDestination()
    {
        var trip = Japan();

        var exception = Assert.Throws<ArgumentException>(() =>
            trip.AddStop(Guid.NewGuid(), "Nowhere"));

        Assert.Equal("parentId", exception.ParamName);
    }

    private static IEnumerable<TripDestinationDraft> OtherParents(Trip trip) =>
        trip.ParentDestinations
            .Skip(1)
            .Select(parent => new TripDestinationDraft(
                parent.Id,
                parent.Name,
                parent.StartsOn,
                parent.EndsOn,
                null,
                null,
                trip.StopsOf(parent.Id)
                    .Select(stop => new TripStopDraft(stop.Id, stop.Name, null, null))
                    .ToArray()));

    private static string Snapshot(Trip trip) =>
        string.Join(
            "|",
            trip.Destinations.Select(destination =>
                $"{destination.Id}:{destination.Name}:{destination.ParentDestinationId}:{destination.SortOrder}"));
}
