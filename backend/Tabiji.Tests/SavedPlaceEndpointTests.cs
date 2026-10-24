using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tabiji.Application.Trips;
using Tabiji.Domain.Trips;

namespace Tabiji.Tests;

/// <summary>
/// The saved-place HTTP surface.
///
/// Every route names the trip as well as the place, so these tests are about
/// two things at once: that the wiring is right, and that a place belonging
/// to another trip is never reachable through it.
/// </summary>
public sealed class SavedPlaceEndpointTests : IDisposable
{
    // The API serialises enums as strings, so the test client needs the same options.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SavedPlaceApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string Route(Guid tripId) => $"/api/trips/{tripId}/places";

    private static CreateSavedPlaceRequest CreateRequest(
        string name = "Kinkaku-ji",
        double? latitude = 35.0394,
        double? longitude = 135.7292,
        PlaceCategory? category = PlaceCategory.Sight,
        string? address = null,
        string? notes = null,
        Guid? destinationId = null) =>
        new(name, latitude, longitude, category, address, notes, destinationId);

    private static UpdateSavedPlaceRequest UpdateRequest(
        string name = "Kinkaku-ji",
        double? latitude = 35.0394,
        double? longitude = 135.7292,
        PlaceCategory? category = PlaceCategory.Sight,
        string? address = null,
        string? notes = null,
        Guid? destinationId = null) =>
        new(name, latitude, longitude, category, address, notes, destinationId);

    [Fact]
    public async Task List_ReturnsThePlacesOfTheTripInTheOrderTheyWereSaved()
    {
        using var client = _factory.CreateClient();

        var places = await client.GetFromJsonAsync<SavedPlaceResponse[]>(
            Route(SavedPlaceApiFactory.Kyoto.Id),
            JsonOptions);

        Assert.NotNull(places);
        Assert.Equal(["Nishiki Market", "Fushimi Inari"], places!.Select(place => place.Name));
    }

    [Fact]
    public async Task List_ReturnsTheFieldsOfAPlace()
    {
        using var client = _factory.CreateClient();

        var places = await client.GetFromJsonAsync<SavedPlaceResponse[]>(
            Route(SavedPlaceApiFactory.Kyoto.Id),
            JsonOptions);

        Assert.NotNull(places);

        var market = places!.Single(place => place.Name == "Nishiki Market");

        Assert.Equal(35.0050, market.Latitude);
        Assert.Equal(135.7649, market.Longitude);
        Assert.Equal(PlaceCategory.Food, market.Category);
        Assert.NotEqual(Guid.Empty, market.Id);
        Assert.Null(market.DestinationId);
    }

    [Fact]
    public async Task List_ReturnsAnEmptyArrayForATripWithNoPlaces()
    {
        using var client = _factory.CreateClient();

        var places = await client.GetFromJsonAsync<SavedPlaceResponse[]>(
            Route(SavedPlaceApiFactory.Rome.Id),
            JsonOptions);

        Assert.NotNull(places);
        Assert.Empty(places!);
    }

    [Fact]
    public async Task List_ReturnsNotFoundForAnUnknownTrip()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(Route(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsThePlaceUnderItsTrip()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var stored = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);
        Assert.NotNull(stored);

        var expected = stored!.First(place => place.Name == "Fushimi Inari");

        var place = await client.GetFromJsonAsync<SavedPlaceResponse>(
            $"{Route(kyoto.Id)}/{expected.Id}",
            JsonOptions);

        Assert.NotNull(place);
        Assert.Equal(expected.Id, place!.Id);
        Assert.Equal(kyoto.Destinations[0].Id, place.DestinationId);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForAnUnknownPlace()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"{Route(SavedPlaceApiFactory.Kyoto.Id)}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForAPlaceOfAnotherTrip()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;

        var stored = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);
        Assert.NotNull(stored);

        var placeId = stored![0].Id;

        // The place exists, but not under this trip.
        var response = await client.GetAsync($"{Route(SavedPlaceApiFactory.Rome.Id)}/{placeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAndThePlaceAppearsInTheList()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;

        var response = await client.PostAsJsonAsync(
            Route(kyoto.Id),
            CreateRequest(address: "1 Kinkakujicho, Kyoto", notes: "Weekday mornings are quiet."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<SavedPlaceResponse>(JsonOptions);
        Assert.NotNull(created);

        Assert.Equal($"{Route(kyoto.Id)}/{created!.Id}", response.Headers.Location?.ToString());

        Assert.Equal("Kinkaku-ji", created.Name);
        Assert.Equal(35.0394, created.Latitude);
        Assert.Equal(135.7292, created.Longitude);
        Assert.Equal(PlaceCategory.Sight, created.Category);
        Assert.Equal("1 Kinkakujicho, Kyoto", created.Address);
        Assert.Equal("Weekday mornings are quiet.", created.Notes);

        var places = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);

        Assert.NotNull(places);
        Assert.Contains(places!, place => place.Id == created.Id);
    }

    [Fact]
    public async Task Create_FilesThePlaceUnderAStopOfTheSameTrip()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var stop = kyoto.Destinations[1].Id;

        var response = await client.PostAsJsonAsync(Route(kyoto.Id), CreateRequest(destinationId: stop));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<SavedPlaceResponse>(JsonOptions);

        Assert.NotNull(created);
        Assert.Equal(stop, created!.DestinationId);
    }

    [Fact]
    public async Task Create_ReturnsNotFoundForAnUnknownTrip()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route(Guid.NewGuid()), CreateRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_ReturnsBadRequestForABlankName(string name)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Route(SavedPlaceApiFactory.Kyoto.Id),
            CreateRequest(name: name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Contains("name is required", problem!.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(0.0, -181.0)]
    public async Task Create_ReturnsBadRequestForAPositionOffTheGlobe(double latitude, double longitude)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Route(SavedPlaceApiFactory.Kyoto.Id),
            CreateRequest(latitude: latitude, longitude: longitude));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null, 135.7727)]
    [InlineData(34.9671, null)]
    [InlineData(null, null)]
    public async Task Create_ReturnsBadRequestWhenThePositionIsMissing(double? latitude, double? longitude)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Route(SavedPlaceApiFactory.Kyoto.Id),
            CreateRequest(latitude: latitude, longitude: longitude));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Contains("is required", problem!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_ReturnsBadRequestForAStopOfAnotherTrip()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var stopOfAnotherTrip = SavedPlaceApiFactory.Rome.Destinations[0].Id;

        var response = await client.PostAsJsonAsync(
            Route(kyoto.Id),
            CreateRequest(destinationId: stopOfAnotherTrip));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Contains("does not belong to this trip", problem!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_ReturnsBadRequestForAStopThatDoesNotExist()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            Route(SavedPlaceApiFactory.Kyoto.Id),
            CreateRequest(destinationId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesThePlaceIncludingItsFiling()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var stored = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);
        Assert.NotNull(stored);

        var placeId = stored![0].Id;
        var stop = kyoto.Destinations[1].Id;

        var response = await client.PutAsJsonAsync(
            $"{Route(kyoto.Id)}/{placeId}",
            UpdateRequest(
                name: "Nishiki Market",
                latitude: 35.0050,
                longitude: 135.7649,
                category: PlaceCategory.Food,
                address: "Kyoto",
                notes: "Closed on Wednesdays.",
                destinationId: stop));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<SavedPlaceResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Nishiki Market", updated!.Name);
        Assert.Equal(PlaceCategory.Food, updated.Category);
        Assert.Equal("Kyoto", updated.Address);
        Assert.Equal("Closed on Wednesdays.", updated.Notes);
        Assert.Equal(stop, updated.DestinationId);
        Assert.Equal(placeId, updated.Id);

        var reread = await client.GetFromJsonAsync<SavedPlaceResponse>(
            $"{Route(kyoto.Id)}/{placeId}",
            JsonOptions);

        Assert.NotNull(reread);
        Assert.Equal(stop, reread!.DestinationId);
    }

    [Fact]
    public async Task Update_TakesThePlaceOffTheRouteWhenNoStopIsGiven()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var stored = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);
        Assert.NotNull(stored);

        var filed = stored!.Single(place => place.DestinationId is not null);

        var response = await client.PutAsJsonAsync(
            $"{Route(kyoto.Id)}/{filed.Id}",
            UpdateRequest(name: filed.Name, destinationId: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<SavedPlaceResponse>(JsonOptions);

        Assert.NotNull(updated);
        Assert.Null(updated!.DestinationId);
    }

    [Fact]
    public async Task Update_ReturnsBadRequestForAStopOfAnotherTripAndLeavesThePlaceUnchanged()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var stored = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);
        Assert.NotNull(stored);

        var before = stored![0];

        var response = await client.PutAsJsonAsync(
            $"{Route(kyoto.Id)}/{before.Id}",
            UpdateRequest(
                name: "Taken over",
                destinationId: SavedPlaceApiFactory.Rome.Destinations[0].Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var reread = await client.GetFromJsonAsync<SavedPlaceResponse>(
            $"{Route(kyoto.Id)}/{before.Id}",
            JsonOptions);

        Assert.NotNull(reread);
        Assert.Equal(before.Name, reread!.Name);
        Assert.Equal(before.DestinationId, reread.DestinationId);
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForAPlaceOfAnotherTripAndLeavesItUnchanged()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var rome = SavedPlaceApiFactory.Rome;

        var stored = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);
        Assert.NotNull(stored);

        var place = stored![0];

        var response = await client.PutAsJsonAsync(
            $"{Route(rome.Id)}/{place.Id}",
            UpdateRequest(name: "Taken over"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var reread = await client.GetFromJsonAsync<SavedPlaceResponse>(
            $"{Route(kyoto.Id)}/{place.Id}",
            JsonOptions);

        Assert.NotNull(reread);
        Assert.Equal(place.Name, reread!.Name);
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForAnUnknownPlace()
    {
        using var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"{Route(SavedPlaceApiFactory.Kyoto.Id)}/{Guid.NewGuid()}",
            UpdateRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesThePlace()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var stored = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);
        Assert.NotNull(stored);

        var placeId = stored![0].Id;

        var response = await client.DeleteAsync($"{Route(kyoto.Id)}/{placeId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var remaining = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);

        Assert.NotNull(remaining);
        Assert.DoesNotContain(remaining!, place => place.Id == placeId);
    }

    [Fact]
    public async Task Delete_ReturnsNotFoundForAPlaceOfAnotherTripAndLeavesIt()
    {
        using var client = _factory.CreateClient();

        var kyoto = SavedPlaceApiFactory.Kyoto;
        var rome = SavedPlaceApiFactory.Rome;

        var stored = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);
        Assert.NotNull(stored);

        var placeId = stored![0].Id;

        var response = await client.DeleteAsync($"{Route(rome.Id)}/{placeId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Kyoto's place is still there.
        var remaining = await client.GetFromJsonAsync<SavedPlaceResponse[]>(Route(kyoto.Id), JsonOptions);

        Assert.NotNull(remaining);
        Assert.Contains(remaining!, place => place.Id == placeId);
    }

    [Fact]
    public async Task Delete_ReturnsNotFoundForAnUnknownPlace()
    {
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync(
            $"{Route(SavedPlaceApiFactory.Kyoto.Id)}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record ProblemDetails(string? Title, string? Detail, int? Status);
}
