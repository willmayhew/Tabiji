using Tabiji.Application.Trips;
using Tabiji.Domain.Trips;
using Tabiji.Tests.Fakes;

namespace Tabiji.Tests;

public sealed class TripTests
{
    private static readonly DateOnly StartsOn = new(2027, 4, 1);
    private static readonly DateOnly EndsOn = new(2027, 4, 14);
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedListOrder = ["Hakone", "Tokyo", "Kyoto"];

    [Fact]
    public void Create_TrimsValuesAndStartsInPlanning()
    {
        var trip = Trip.Create("  Tokyo & Kyoto  ", "  Japan  ", StartsOn, EndsOn, CreatedAt);

        Assert.Equal("Tokyo & Kyoto", trip.Name);
        Assert.Equal("Japan", trip.Destination);
        Assert.Equal(TripStatus.Planning, trip.Status);
        Assert.Equal(StartsOn, trip.StartsOn);
        Assert.Equal(EndsOn, trip.EndsOn);
        Assert.NotEqual(Guid.Empty, trip.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankName(string name)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create(name, "Japan", StartsOn, EndsOn, CreatedAt));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsNameLongerThanTheLimit()
    {
        var name = new string('x', Trip.NameMaxLength + 1);

        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create(name, "Japan", StartsOn, EndsOn, CreatedAt));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsBlankDestination()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create("Tokyo", "  ", StartsOn, EndsOn, CreatedAt));

        Assert.Equal("destination", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsEndDateBeforeStartDate()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Trip.Create("Tokyo", "Japan", EndsOn, StartsOn, CreatedAt));

        Assert.Equal("endsOn", exception.ParamName);
    }

    [Fact]
    public void Reschedule_RejectsEndDateBeforeStartDate()
    {
        var trip = Trip.Create("Tokyo", "Japan", StartsOn, EndsOn, CreatedAt);

        var exception = Assert.Throws<ArgumentException>(() => trip.Reschedule(EndsOn, StartsOn));

        Assert.Equal("endsOn", exception.ParamName);
    }

    [Fact]
    public void Reschedule_UpdatesDates()
    {
        var trip = Trip.Create("Tokyo", "Japan", StartsOn, EndsOn, CreatedAt);

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
            new CreateTripRequest("Tokyo & Kyoto", "Japan", StartsOn, EndsOn));

        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal("Tokyo & Kyoto", response.Name);
        Assert.Equal("Japan", response.Destination);
        Assert.Equal(TripStatus.Planning, response.Status);
        Assert.Equal(StartsOn, response.StartsOn);
        Assert.Equal(EndsOn, response.EndsOn);
    }

    [Fact]
    public async Task GetAsync_ReturnsTheStoredTrip()
    {
        var repository = new FakeTripRepository();
        var service = new TripService(repository, new FixedTimeProvider(CreatedAt));

        var created = await service.CreateAsync(
            new CreateTripRequest("Tokyo & Kyoto", "Japan", StartsOn, EndsOn));

        var fetched = await service.GetAsync(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Name, fetched.Name);
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
            Trip.Create("Kyoto", "Kansai", new DateOnly(2027, 4, 10), new DateOnly(2027, 4, 14), CreatedAt),
            Trip.Create("Tokyo", "Kanto", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 8), CreatedAt),
            Trip.Create("Hakone", "Kanto", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 3), CreatedAt)
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
            Trip.Create("Tokyo & Kyoto", "Japan", StartsOn, EndsOn, CreatedAt)
        ]);

        var service = new TripService(repository, TimeProvider.System);

        var trips = await service.ListAsync();

        var trip = Assert.Single(trips);
        Assert.Equal("Tokyo & Kyoto", trip.Name);
        Assert.Equal("Japan", trip.Destination);
        Assert.Equal(StartsOn, trip.StartsOn);
        Assert.Equal(EndsOn, trip.EndsOn);
        Assert.Equal(TripStatus.Planning, trip.Status);
    }
}
