using Tabiji.Application.Trips;
using Tabiji.Domain.Trips;
using Tabiji.Tests.Fakes;

namespace Tabiji.Tests;

/// <summary>
/// The saved-place use cases, run against in-memory fakes.
///
/// The rules a place can answer on its own live in the domain and are checked
/// there. What is checked here is the pair of questions a place cannot answer
/// alone: whether the trip exists, and whether a stop it is filed under
/// really belongs to that trip.
/// </summary>
public sealed class SavedPlaceServiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private sealed record Harness(
        SavedPlaceService Service,
        FakeSavedPlaceRepository Places,
        FakeTripRepository Trips);

    private static Trip Kyoto() => Trip.Create(
        "Kyoto",
        [TripDestinationDraft.Undated("Kyoto"), TripDestinationDraft.Undated("Nara")],
        new DateOnly(2027, 4, 10),
        new DateOnly(2027, 4, 14),
        CreatedAt);

    private static Trip Rome() => Trip.Create(
        "Rome",
        [TripDestinationDraft.Undated("Rome")],
        new DateOnly(2027, 4, 1),
        new DateOnly(2027, 4, 8),
        CreatedAt);

    private static Harness Build(params Trip[] seed)
    {
        var places = new FakeSavedPlaceRepository();
        var trips = new FakeTripRepository(seed);

        return new Harness(
            new SavedPlaceService(places, trips, new FixedTimeProvider(CreatedAt)),
            places,
            trips);
    }

    private static CreateSavedPlaceRequest CreateRequest(
        string name = "Fushimi Inari",
        double? latitude = 34.9671,
        double? longitude = 135.7727,
        PlaceCategory? category = PlaceCategory.Sight,
        string? address = null,
        string? notes = null,
        Guid? destinationId = null) =>
        new(name, latitude, longitude, category, address, notes, destinationId);

    private static UpdateSavedPlaceRequest UpdateRequest(
        string name = "Fushimi Inari",
        double? latitude = 34.9671,
        double? longitude = 135.7727,
        PlaceCategory? category = PlaceCategory.Sight,
        string? address = null,
        string? notes = null,
        Guid? destinationId = null) =>
        new(name, latitude, longitude, category, address, notes, destinationId);

    private static SavedPlace Place(
        Trip trip,
        string name = "Fushimi Inari",
        DateTimeOffset? createdAt = null,
        Guid? destinationId = null)
    {
        var place = SavedPlace.Create(
            trip.Id,
            name,
            34.9671,
            135.7727,
            PlaceCategory.Sight,
            null,
            null,
            createdAt ?? CreatedAt);

        place.MoveToDestination(destinationId);

        return place;
    }

    [Fact]
    public async Task CreateAsync_SavesThePlaceToTheTrip()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);

        var created = await harness.Service.CreateAsync(
            kyoto.Id,
            CreateRequest(notes: "Go early.", address: "Kyoto"));

        Assert.NotNull(created);
        Assert.Equal(kyoto.Id, (await harness.Places.GetByIdAsync(created!.Id))!.TripId);
        Assert.Equal("Fushimi Inari", created.Name);
        Assert.Equal(34.9671, created.Latitude);
        Assert.Equal(135.7727, created.Longitude);
        Assert.Equal(PlaceCategory.Sight, created.Category);
        Assert.Equal("Kyoto", created.Address);
        Assert.Equal("Go early.", created.Notes);
        Assert.Equal(CreatedAt, created.CreatedAt);
        Assert.Null(created.DestinationId);
        Assert.Equal(1, harness.Places.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNullWhenThereIsNoSuchTrip()
    {
        var harness = Build(Kyoto());

        var created = await harness.Service.CreateAsync(Guid.NewGuid(), CreateRequest());

        Assert.Null(created);
        Assert.Equal(0, harness.Places.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_FilesThePlaceUnderAStopOfTheSameTrip()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);
        var stop = kyoto.Destinations[1].Id;

        var created = await harness.Service.CreateAsync(
            kyoto.Id,
            CreateRequest(destinationId: stop));

        Assert.NotNull(created);
        Assert.Equal(stop, created!.DestinationId);
    }

    [Fact]
    public async Task CreateAsync_RejectsAStopOfAnotherTrip()
    {
        var kyoto = Kyoto();
        var rome = Rome();
        var harness = Build(kyoto, rome);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Service.CreateAsync(
                kyoto.Id,
                CreateRequest(destinationId: rome.Destinations[0].Id)));

        Assert.Equal("destinationId", exception.ParamName);
        Assert.Equal(0, harness.Places.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsAStopThatDoesNotExist()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Service.CreateAsync(
                kyoto.Id,
                CreateRequest(destinationId: Guid.NewGuid())));

        Assert.Equal(0, harness.Places.AddCallCount);
    }

    [Theory]
    [InlineData(null, 135.7727)]
    [InlineData(34.9671, null)]
    [InlineData(null, null)]
    public async Task CreateAsync_RejectsAMissingPosition(double? latitude, double? longitude)
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Service.CreateAsync(
                kyoto.Id,
                CreateRequest(latitude: latitude, longitude: longitude)));

        Assert.Equal(0, harness.Places.AddCallCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsAPositionOffTheGlobe()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Service.CreateAsync(kyoto.Id, CreateRequest(latitude: 91.0)));

        Assert.Equal(0, harness.Places.AddCallCount);
    }

    [Fact]
    public async Task ListAsync_ReturnsThePlacesOfTheTripInTheOrderTheyWereSaved()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);

        await harness.Service.CreateAsync(kyoto.Id, CreateRequest(name: "Nishiki Market"));
        await harness.Places.AddAsync(Place(kyoto, "Fushimi Inari", CreatedAt.AddHours(1)));

        var places = await harness.Service.ListAsync(kyoto.Id);

        Assert.NotNull(places);
        Assert.Equal(["Nishiki Market", "Fushimi Inari"], places!.Select(place => place.Name));
    }

    [Fact]
    public async Task ListAsync_NeverReturnsThePlacesOfAnotherTrip()
    {
        var kyoto = Kyoto();
        var rome = Rome();
        var harness = Build(kyoto, rome);

        await harness.Places.AddAsync(Place(rome, "Colosseum"));
        await harness.Places.AddAsync(Place(kyoto, "Fushimi Inari"));

        var places = await harness.Service.ListAsync(kyoto.Id);

        Assert.NotNull(places);
        Assert.Equal(["Fushimi Inari"], places!.Select(place => place.Name));
    }

    [Fact]
    public async Task ListAsync_ReturnsAnEmptyListForATripWithNoPlaces()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);

        var places = await harness.Service.ListAsync(kyoto.Id);

        Assert.NotNull(places);
        Assert.Empty(places!);
    }

    [Fact]
    public async Task ListAsync_ReturnsNullWhenThereIsNoSuchTrip()
    {
        var harness = Build(Kyoto());

        Assert.Null(await harness.Service.ListAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAsync_ReturnsThePlaceOfTheTrip()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);
        var place = Place(kyoto);

        await harness.Places.AddAsync(place);

        var found = await harness.Service.GetAsync(kyoto.Id, place.Id);

        Assert.NotNull(found);
        Assert.Equal(place.Id, found!.Id);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForAPlaceOfAnotherTrip()
    {
        var kyoto = Kyoto();
        var rome = Rome();
        var harness = Build(kyoto, rome);
        var place = Place(rome);

        await harness.Places.AddAsync(place);

        // The place exists, but not for this trip, so the answer is the same
        // as for a place that does not exist at all.
        Assert.Null(await harness.Service.GetAsync(kyoto.Id, place.Id));
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForAnUnknownPlace()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);

        Assert.Null(await harness.Service.GetAsync(kyoto.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheFields()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);
        var place = Place(kyoto);

        await harness.Places.AddAsync(place);

        var updated = await harness.Service.UpdateAsync(
            kyoto.Id,
            place.Id,
            UpdateRequest(
                name: "Nishiki Market",
                latitude: 35.0050,
                longitude: 135.7649,
                category: PlaceCategory.Food,
                address: "Kyoto",
                notes: "Closed on Wednesdays."));

        Assert.NotNull(updated);
        Assert.Equal("Nishiki Market", updated!.Name);
        Assert.Equal(35.0050, updated.Latitude);
        Assert.Equal(135.7649, updated.Longitude);
        Assert.Equal(PlaceCategory.Food, updated.Category);
        Assert.Equal("Kyoto", updated.Address);
        Assert.Equal("Closed on Wednesdays.", updated.Notes);
        Assert.Equal(CreatedAt, updated.CreatedAt);
        Assert.Equal(1, harness.Places.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateAsync_FilesThePlaceUnderAStop()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);
        var place = Place(kyoto);
        var stop = kyoto.Destinations[1].Id;

        await harness.Places.AddAsync(place);

        var updated = await harness.Service.UpdateAsync(
            kyoto.Id,
            place.Id,
            UpdateRequest(destinationId: stop));

        Assert.NotNull(updated);
        Assert.Equal(stop, updated!.DestinationId);
    }

    [Fact]
    public async Task UpdateAsync_TakesThePlaceOffTheRouteWhenNoStopIsGiven()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);
        var place = Place(kyoto, destinationId: kyoto.Destinations[0].Id);

        await harness.Places.AddAsync(place);

        var updated = await harness.Service.UpdateAsync(
            kyoto.Id,
            place.Id,
            UpdateRequest(destinationId: null));

        Assert.NotNull(updated);
        Assert.Null(updated!.DestinationId);
    }

    [Fact]
    public async Task UpdateAsync_RejectsAStopOfAnotherTrip()
    {
        var kyoto = Kyoto();
        var rome = Rome();
        var harness = Build(kyoto, rome);
        var place = Place(kyoto);

        await harness.Places.AddAsync(place);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Service.UpdateAsync(
                kyoto.Id,
                place.Id,
                UpdateRequest(destinationId: rome.Destinations[0].Id)));

        Assert.Equal("destinationId", exception.ParamName);

        // The place is left exactly as it was.
        Assert.Equal(0, harness.Places.UpdateCallCount);
        Assert.Null((await harness.Places.GetByIdAsync(place.Id))!.DestinationId);
    }

    [Fact]
    public async Task UpdateAsync_RejectsAStopThatIsNoLongerOnTheTrip()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);
        var place = Place(kyoto, destinationId: kyoto.Destinations[0].Id);

        await harness.Places.AddAsync(place);

        var removed = kyoto.Destinations[1].Id;
        kyoto.RemoveDestination(removed);

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Service.UpdateAsync(kyoto.Id, place.Id, UpdateRequest(destinationId: removed)));

        Assert.Equal(0, harness.Places.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForAPlaceOfAnotherTrip()
    {
        var kyoto = Kyoto();
        var rome = Rome();
        var harness = Build(kyoto, rome);
        var place = Place(rome);

        await harness.Places.AddAsync(place);

        var updated = await harness.Service.UpdateAsync(
            kyoto.Id,
            place.Id,
            UpdateRequest(name: "Taken over"));

        Assert.Null(updated);
        Assert.Equal(0, harness.Places.UpdateCallCount);

        // Rome's place is untouched.
        Assert.Equal("Fushimi Inari", (await harness.Places.GetByIdAsync(place.Id))!.Name);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenThereIsNoSuchTrip()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);
        var place = Place(kyoto);

        await harness.Places.AddAsync(place);

        Assert.Null(await harness.Service.UpdateAsync(Guid.NewGuid(), place.Id, UpdateRequest()));
        Assert.Equal(0, harness.Places.UpdateCallCount);
    }

    [Fact]
    public async Task DeleteAsync_RemovesThePlace()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);
        var place = Place(kyoto);

        await harness.Places.AddAsync(place);

        Assert.True(await harness.Service.DeleteAsync(kyoto.Id, place.Id));
        Assert.Equal(1, harness.Places.RemoveCallCount);
        Assert.Null(await harness.Places.GetByIdAsync(place.Id));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForAPlaceOfAnotherTrip()
    {
        var kyoto = Kyoto();
        var rome = Rome();
        var harness = Build(kyoto, rome);
        var place = Place(rome);

        await harness.Places.AddAsync(place);

        Assert.False(await harness.Service.DeleteAsync(kyoto.Id, place.Id));
        Assert.Equal(0, harness.Places.RemoveCallCount);

        // Rome's place is still there.
        Assert.NotNull(await harness.Places.GetByIdAsync(place.Id));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForAnUnknownPlace()
    {
        var kyoto = Kyoto();
        var harness = Build(kyoto);

        Assert.False(await harness.Service.DeleteAsync(kyoto.Id, Guid.NewGuid()));
        Assert.Equal(0, harness.Places.RemoveCallCount);
    }
}
