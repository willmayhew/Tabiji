using Tabiji.Application.Trips;
using Tabiji.Domain.Trips;
using Tabiji.Tests.Fakes;

namespace Tabiji.Tests;

public sealed class TripTests
{
    private static readonly DateOnly StartsOn = new(2027, 4, 1);
    private static readonly DateOnly EndsOn = new(2027, 4, 14);
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedListOrder = ["Reykjavík", "Rome", "Kyoto"];

    private static readonly TripDestinationDraft[] Route =
    [
        TripDestinationDraft.Undated("Rome"),
        TripDestinationDraft.Undated("Amalfi"),
        TripDestinationDraft.Undated("Naples")
    ];

    private static readonly TripDestinationRequest[] RouteRequest =
    [
        new("Rome", null, null),
        new("Amalfi", null, null),
        new("Naples", null, null)
    ];

    private static readonly TripDestinationUpdateRequest[] RouteUpdateRequest =
    [
        new(null, "Rome", null, null),
        new(null, "Amalfi", null, null),
        new(null, "Naples", null, null)
    ];

    private static TripDestinationDraft[] Stops(params string[] names) =>
        [.. names.Select(name => TripDestinationDraft.Undated(name))];

    [Fact]
    public void Create_TrimsValuesAndStartsInPlanning()
    {
        var trip = Trip.Create("  Rome & Amalfi  ", Stops("  Italy  "), StartsOn, EndsOn, CreatedAt);

        Assert.Equal("Rome & Amalfi", trip.Name);
        Assert.Equal("Italy", Assert.Single(trip.Destinations).Name);
        Assert.Equal(TripStatus.Planning, trip.Status);
        Assert.Equal(StartsOn, trip.StartsOn);
        Assert.Equal(EndsOn, trip.EndsOn);
        Assert.NotEqual(Guid.Empty, trip.Id);
    }

    [Fact]
    public void Create_AddsTheDestinationsInOrder()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);

        Assert.Equal(["Rome", "Amalfi", "Naples"], trip.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2], trip.Destinations.Select(d => d.SortOrder));
        Assert.All(trip.Destinations, destination => Assert.Equal(trip.Id, destination.TripId));
    }

    [Fact]
    public void Create_TrimsEachDestinationName()
    {
        var trip = Trip.Create("Rome & Amalfi", Stops("  Rome  ", " Amalfi "), StartsOn, EndsOn, CreatedAt);

        Assert.Equal(["Rome", "Amalfi"], trip.Destinations.Select(d => d.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankName(string name)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create(name, Route, StartsOn, EndsOn, CreatedAt));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsNameLongerThanTheLimit()
    {
        var name = new string('x', Trip.NameMaxLength + 1);

        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create(name, Route, StartsOn, EndsOn, CreatedAt));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsAnEmptyDestinationList()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create("Rome & Amalfi", [], StartsOn, EndsOn, CreatedAt));

        Assert.Equal("destinations", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsTooManyDestinations()
    {
        var destinations = Enumerable.Range(1, Trip.MaxDestinationCount + 1)
            .Select(index => TripDestinationDraft.Undated($"Stop {index}"))
            .ToArray();

        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create("Too many stops", destinations, StartsOn, EndsOn, CreatedAt));

        Assert.Equal("destinations", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankDestinationName(string destination)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create("Rome & Amalfi", Stops("Rome", destination), StartsOn, EndsOn, CreatedAt));

        Assert.Equal("destinations", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsADestinationNameLongerThanTheLimit()
    {
        var destination = new string('x', TripDestination.NameMaxLength + 1);

        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create("Rome & Amalfi", Stops("Rome", destination), StartsOn, EndsOn, CreatedAt));

        Assert.Equal("destinations", exception.ParamName);
    }

    [Fact]
    public void Create_AllowsTheSameDestinationTwice()
    {
        var trip = Trip.Create("A round trip", Stops("Rome", "Florence", "Rome"), StartsOn, EndsOn, CreatedAt);

        Assert.Equal(["Rome", "Florence", "Rome"], trip.Destinations.Select(d => d.Name));
    }

    [Fact]
    public void Create_RejectsEndDateBeforeStartDate()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create("Rome & Amalfi", Route, EndsOn, StartsOn, CreatedAt));

        Assert.Equal("endsOn", exception.ParamName);
    }

    [Fact]
    public void AddDestination_AppendsAtTheEnd()
    {
        var trip = Trip.Create("Rome & Amalfi", Stops("Rome"), StartsOn, EndsOn, CreatedAt);

        trip.AddDestination("Naples");

        Assert.Equal(["Rome", "Naples"], trip.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1], trip.Destinations.Select(d => d.SortOrder));
    }

    [Fact]
    public void AddDestination_RejectsTooManyDestinations()
    {
        var destinations = Enumerable.Range(1, Trip.MaxDestinationCount)
            .Select(index => TripDestinationDraft.Undated($"Stop {index}"))
            .ToArray();

        var trip = Trip.Create("At the limit", destinations, StartsOn, EndsOn, CreatedAt);

        var exception = Assert.Throws<ArgumentException>(() => trip.AddDestination("One too many"));

        Assert.Equal("destinations", exception.ParamName);
    }

    [Fact]
    public void RemoveDestination_RenumbersTheRemainingStops()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);
        var removed = trip.Destinations[1].Id;

        trip.RemoveDestination(removed);

        Assert.Equal(["Rome", "Naples"], trip.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1], trip.Destinations.Select(d => d.SortOrder));
    }

    [Fact]
    public void RemoveDestination_RejectsRemovingTheLastOne()
    {
        var trip = Trip.Create("Rome & Amalfi", Stops("Rome"), StartsOn, EndsOn, CreatedAt);

        var exception = Assert.Throws<ArgumentException>(
            () => trip.RemoveDestination(trip.Destinations[0].Id));

        Assert.Equal("destinationId", exception.ParamName);
    }

    [Fact]
    public void RemoveDestination_RejectsAnUnknownDestination()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);

        var exception = Assert.Throws<ArgumentException>(
            () => trip.RemoveDestination(Guid.NewGuid()));

        Assert.Equal("destinationId", exception.ParamName);
    }

    [Fact]
    public void ReorderDestinations_RewritesTheSortOrder()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);
        var ids = trip.Destinations.Select(destination => destination.Id).ToArray();

        trip.ReorderDestinations([ids[2], ids[0], ids[1]]);

        Assert.Equal(["Naples", "Rome", "Amalfi"], trip.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2], trip.Destinations.Select(d => d.SortOrder));
    }

    [Fact]
    public void ReorderDestinations_RejectsAnOrderThatIsNotTheWholeRoute()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);
        var ids = trip.Destinations.Select(destination => destination.Id).ToArray();

        var exception = Assert.Throws<ArgumentException>(
            () => trip.ReorderDestinations([ids[0], ids[1]]));

        Assert.Equal("orderedIds", exception.ParamName);
    }

    [Fact]
    public void ReorderDestinations_RejectsAnUnknownDestination()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);
        var ids = trip.Destinations.Select(d => d.Id).ToList();
        ids[0] = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() => trip.ReorderDestinations(ids));

        Assert.Equal("orderedIds", exception.ParamName);
    }

    [Fact]
    public void Reschedule_RejectsEndDateBeforeStartDate()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);

        var exception = Assert.Throws<ArgumentException>(() => trip.Reschedule(EndsOn, StartsOn));

        Assert.Equal("endsOn", exception.ParamName);
    }

    [Fact]
    public void Reschedule_UpdatesDates()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);

        var newStartsOn = StartsOn.AddDays(7);
        var newEndsOn = EndsOn.AddDays(7);

        trip.Reschedule(newStartsOn, newEndsOn);

        Assert.Equal(newStartsOn, trip.StartsOn);
        Assert.Equal(newEndsOn, trip.EndsOn);
    }

    [Fact]
    public async Task CreateAsync_PersistsTheTripAndReturnsAResponse()
    {
        var repository = new FakeTripRepository();
        var service = new TripService(repository, new FixedTimeProvider(CreatedAt));

        var response = await service.CreateAsync(
            new CreateTripRequest("Rome & Amalfi", RouteRequest, StartsOn, EndsOn));

        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal("Rome & Amalfi", response.Name);
        Assert.Equal(["Rome", "Amalfi", "Naples"], response.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2], response.Destinations.Select(d => d.SortOrder));
        Assert.Equal(TripStatus.Planning, response.Status);
        Assert.Equal(StartsOn, response.StartsOn);
        Assert.Equal(EndsOn, response.EndsOn);
    }

    [Fact]
    public async Task CreateAsync_RejectsATripWithoutDestinations()
    {
        var repository = new FakeTripRepository();
        var service = new TripService(repository, new FixedTimeProvider(CreatedAt));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(new CreateTripRequest("Rome & Amalfi", [], StartsOn, EndsOn)));
    }

    [Fact]
    public async Task GetAsync_ReturnsTheStoredTrip()
    {
        var repository = new FakeTripRepository();
        var service = new TripService(repository, new FixedTimeProvider(CreatedAt));

        var created = await service.CreateAsync(
            new CreateTripRequest("Rome & Amalfi", RouteRequest, StartsOn, EndsOn));

        var fetched = await service.GetAsync(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Name, fetched.Name);
        Assert.Equal(["Rome", "Amalfi", "Naples"], fetched.Destinations.Select(d => d.Name));
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenTheTripDoesNotExist()
    {
        var service = new TripService(new FakeTripRepository(), TimeProvider.System);

        var response = await service.GetAsync(Guid.NewGuid());

        Assert.Null(response);
    }

    [Fact]
    public async Task ListAsync_ReturnsTripsOrderedByStartDateThenName()
    {
        var repository = new FakeTripRepository(
        [
            Trip.Create("Kyoto", Stops("Kyoto", "Nara", "Osaka"), new DateOnly(2027, 4, 10), new DateOnly(2027, 4, 14), CreatedAt),
            Trip.Create("Rome", Stops("Rome", "Amalfi", "Naples"), new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 8), CreatedAt),
            Trip.Create("Reykjavík", Stops("Reykjavík", "Vík"), new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 3), CreatedAt)
        ]);

        var service = new TripService(repository, TimeProvider.System);

        var trips = await service.ListAsync();

        Assert.Equal(ExpectedListOrder, trips.Select(trip => trip.Name));
    }

    [Fact]
    public async Task ListAsync_ReturnsAnEmptyListWhenThereAreNoTrips()
    {
        var service = new TripService(new FakeTripRepository(), TimeProvider.System);

        var trips = await service.ListAsync();

        Assert.Empty(trips);
    }

    [Fact]
    public async Task ListAsync_MapsEveryTripToAResponse()
    {
        var repository = new FakeTripRepository(
        [
            Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt)
        ]);

        var service = new TripService(repository, TimeProvider.System);

        var trips = await service.ListAsync();

        var trip = Assert.Single(trips);
        Assert.Equal("Rome & Amalfi", trip.Name);
        Assert.Equal(["Rome", "Amalfi", "Naples"], trip.Destinations.Select(d => d.Name));
        Assert.Equal(StartsOn, trip.StartsOn);
        Assert.Equal(EndsOn, trip.EndsOn);
        Assert.Equal(TripStatus.Planning, trip.Status);
    }

    [Fact]
    public void ApplyRoute_RenamesReschedulesAndRebuildsTheRoute()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);
        var rome = trip.Destinations[0].Id;

        trip.ApplyRoute(
            "Italy",
            StartsOn.AddDays(1),
            EndsOn.AddDays(1),
            [
                new TripDestinationDraft(rome, "Roma", new DateOnly(2027, 4, 2), new DateOnly(2027, 4, 4)),
                new TripDestinationDraft(null, "Pompeii", null, null)
            ]);

        Assert.Equal("Italy", trip.Name);
        Assert.Equal(StartsOn.AddDays(1), trip.StartsOn);
        Assert.Equal(EndsOn.AddDays(1), trip.EndsOn);
        Assert.Equal(["Roma", "Pompeii"], trip.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1], trip.Destinations.Select(d => d.SortOrder));
        Assert.Equal(rome, trip.Destinations[0].Id);
        Assert.Equal(new DateOnly(2027, 4, 2), trip.Destinations[0].StartsOn);
        Assert.Equal(new DateOnly(2027, 4, 4), trip.Destinations[0].EndsOn);
        Assert.Null(trip.Destinations[1].StartsOn);
        Assert.Null(trip.Destinations[1].EndsOn);
        Assert.NotEqual(Guid.Empty, trip.Destinations[1].Id);
    }

    [Fact]
    public void ApplyRoute_ClearsTheDatesOfAStop()
    {
        var trip = Trip.Create(
            "Japan",
            [new TripDestinationDraft(null, "Osaka", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 4))],
            StartsOn,
            EndsOn,
            CreatedAt);
        var osaka = trip.Destinations[0].Id;

        trip.ApplyRoute(
            "Japan",
            StartsOn,
            EndsOn,
            [new TripDestinationDraft(osaka, "Osaka", null, null)]);

        Assert.Null(trip.Destinations[0].StartsOn);
        Assert.Null(trip.Destinations[0].EndsOn);
    }

    [Fact]
    public void ApplyRoute_ChangesNothingWhenADateIsOutsideTheTrip()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);
        var before = trip.Destinations.Select(d => (d.Id, d.Name, d.SortOrder)).ToArray();

        Assert.Throws<ArgumentException>(() => trip.ApplyRoute(
            "Changed",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(null, "Osaka", StartsOn.AddDays(-1), null),
                new TripDestinationDraft(null, "Kyoto", null, null)
            ]));

        Assert.Equal("Rome & Amalfi", trip.Name);
        Assert.Equal(StartsOn, trip.StartsOn);
        Assert.Equal(EndsOn, trip.EndsOn);
        Assert.Equal(before, trip.Destinations.Select(d => (d.Id, d.Name, d.SortOrder)).ToArray());
    }

    [Fact]
    public void ApplyRoute_ChangesNothingWhenTheDatesContradictTheOrder()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);
        var before = trip.Destinations.Select(d => (d.Id, d.Name, d.SortOrder)).ToArray();

        Assert.Throws<ArgumentException>(() => trip.ApplyRoute(
            "Changed",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(null, "Osaka", StartsOn, StartsOn.AddDays(3)),
                new TripDestinationDraft(null, "Kyoto", StartsOn.AddDays(1), StartsOn.AddDays(5))
            ]));

        Assert.Equal("Rome & Amalfi", trip.Name);
        Assert.Equal(before, trip.Destinations.Select(d => (d.Id, d.Name, d.SortOrder)).ToArray());
    }

    [Fact]
    public void ApplyRoute_RejectsADestinationTheTripDoesNotHave()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);

        var exception = Assert.Throws<ArgumentException>(() => trip.ApplyRoute(
            "Rome & Amalfi",
            StartsOn,
            EndsOn,
            [new TripDestinationDraft(Guid.NewGuid(), "Osaka", null, null)]));

        Assert.Equal("destinations", exception.ParamName);
    }

    [Fact]
    public void ApplyRoute_RejectsTheSameDestinationTwice()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);
        var rome = trip.Destinations[0].Id;

        var exception = Assert.Throws<ArgumentException>(() => trip.ApplyRoute(
            "Rome & Amalfi",
            StartsOn,
            EndsOn,
            [
                new TripDestinationDraft(rome, "Rome", null, null),
                new TripDestinationDraft(rome, "Roma", null, null)
            ]));

        Assert.Equal("destinations", exception.ParamName);
    }

    [Fact]
    public void ApplyRoute_RejectsAnEmptyRoute()
    {
        var trip = Trip.Create("Rome & Amalfi", Route, StartsOn, EndsOn, CreatedAt);

        var exception = Assert.Throws<ArgumentException>(() => trip.ApplyRoute(
            "Rome & Amalfi",
            StartsOn,
            EndsOn,
            []));

        Assert.Equal("destinations", exception.ParamName);
    }

    [Fact]
    public async Task UpdateAsync_AppliesTheWholeTrip()
    {
        var repository = new FakeTripRepository();
        var service = new TripService(repository, new FixedTimeProvider(CreatedAt));

        var created = await service.CreateAsync(
            new CreateTripRequest("Rome & Amalfi", RouteRequest, StartsOn, EndsOn));

        var response = await service.UpdateAsync(
            created.Id,
            new UpdateTripRequest(
                "Italy",
                [
                    new(created.Destinations[2].Id, "Napoli", StartsOn, StartsOn.AddDays(2)),
                    new(null, "Pompeii", null, null)
                ],
                StartsOn,
                EndsOn));

        Assert.NotNull(response);
        Assert.Equal("Italy", response!.Name);
        Assert.Equal(["Napoli", "Pompeii"], response.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1], response.Destinations.Select(d => d.SortOrder));
        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenTheTripDoesNotExist()
    {
        var service = new TripService(new FakeTripRepository(), TimeProvider.System);

        var response = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateTripRequest("Rome & Amalfi", RouteUpdateRequest, StartsOn, EndsOn));

        Assert.Null(response);
    }

    [Fact]
    public async Task UpdateAsync_LeavesTheTripUnchangedWhenTheRouteIsRefused()
    {
        var repository = new FakeTripRepository();
        var service = new TripService(repository, new FixedTimeProvider(CreatedAt));

        var created = await service.CreateAsync(
            new CreateTripRequest("Rome & Amalfi", RouteRequest, StartsOn, EndsOn));

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(
            created.Id,
            new UpdateTripRequest(
                "Changed",
                [new(created.Destinations[0].Id, "Roma", StartsOn.AddDays(-1), null)],
                StartsOn,
                EndsOn)));

        var stored = await repository.GetByIdAsync(created.Id);

        Assert.NotNull(stored);
        Assert.Equal("Rome & Amalfi", stored!.Name);
        Assert.Equal(["Rome", "Amalfi", "Naples"], stored.Destinations.Select(d => d.Name));
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
