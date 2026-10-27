using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tabiji.Application.Trips;
using Tabiji.Domain.Trips;

namespace Tabiji.Tests;

/// <summary>
/// The read-only saved-place compatibility routes.
///
/// Places are rows of the trip's hierarchy now, and the trip API is the only
/// writer. These tests prove that the old read routes still expose the places
/// that carry a position, and that the retired write routes are gone.
/// </summary>
public sealed class SavedPlaceEndpointTests : IDisposable
{
    private static readonly DateOnly StartsOn = new(2027, 4, 10);
    private static readonly DateOnly EndsOn = new(2027, 4, 14);
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    // The API serialises enums as strings, so the test client needs the same options.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static Trip KyotoWithPlaces() => Trip.Create(
        "Kyoto",
        [
            new TripDestinationDraft(
                null,
                "Kyoto",
                StartsOn,
                EndsOn,
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
                        new DateOnly(2027, 4, 11)),
                    new TripStopDraft(
                        null,
                        "Arashiyama",
                        null,
                        null,
                        null,
                        null,
                        PlaceCategory.Sight,
                        null,
                        null,
                        new DateOnly(2027, 4, 12)),
                ]),
        ],
        StartsOn,
        EndsOn,
        CreatedAt);

    private static Trip Rome() => Trip.Create(
        "Rome",
        [new TripDestinationDraft(null, "Rome", StartsOn, EndsOn)],
        StartsOn,
        EndsOn,
        CreatedAt);

    private readonly Trip _kyoto = KyotoWithPlaces();
    private readonly Trip _rome = Rome();
    private readonly TripApiFactory _factory;

    public SavedPlaceEndpointTests()
    {
        _factory = new TripApiFactory([_kyoto, _rome]);
    }

    public void Dispose() => _factory.Dispose();

    private static string Route(Guid tripId) => $"/api/trips/{tripId}/places";

    private Guid GionId() => _kyoto.StopsOf(_kyoto.ParentDestinations[0].Id)[0].Id;

    private Guid ArashiyamaId() => _kyoto.StopsOf(_kyoto.ParentDestinations[0].Id)[1].Id;

    [Fact]
    public async Task List_ReturnsOnlyThePlacesThatCarryAPosition()
    {
        using var client = _factory.CreateClient();

        var places = await client.GetFromJsonAsync<SavedPlaceResponse[]>(
            Route(_kyoto.Id),
            JsonOptions);

        Assert.NotNull(places);
        var place = Assert.Single(places!);

        Assert.Equal("Gion", place.Name);
        Assert.Equal(GionId(), place.Id);
        Assert.Equal(_kyoto.ParentDestinations[0].Id, place.DestinationId);
        Assert.Equal(35.0037, place.Latitude);
        Assert.Equal(135.7788, place.Longitude);
        Assert.Equal(PlaceCategory.Sight, place.Category);
        Assert.Equal("Gion, Kyoto", place.Address);
        Assert.Equal("Go early.", place.Notes);
        Assert.Equal(new DateOnly(2027, 4, 11), place.PlannedOn);
        Assert.Equal(CreatedAt, place.CreatedAt);
    }

    [Fact]
    public async Task List_ReturnsAnEmptyArrayForATripWithNoPlaces()
    {
        using var client = _factory.CreateClient();

        var places = await client.GetFromJsonAsync<SavedPlaceResponse[]>(
            Route(_rome.Id),
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

        var place = await client.GetFromJsonAsync<SavedPlaceResponse>(
            $"{Route(_kyoto.Id)}/{GionId()}",
            JsonOptions);

        Assert.NotNull(place);
        Assert.Equal(GionId(), place!.Id);
        Assert.Equal("Gion", place.Name);
        Assert.Equal(_kyoto.ParentDestinations[0].Id, place.DestinationId);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForAPlaceWithoutAPosition()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"{Route(_kyoto.Id)}/{ArashiyamaId()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForAnUnknownPlace()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"{Route(_kyoto.Id)}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForAPlaceOfAnotherTrip()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"{Route(_rome.Id)}/{GionId()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WriteRoutes_AreRetired()
    {
        using var client = _factory.CreateClient();

        var post = await client.PostAsJsonAsync(Route(_kyoto.Id), new { name = "Kinkaku-ji" });
        var put = await client.PutAsJsonAsync(
            $"{Route(_kyoto.Id)}/{GionId()}",
            new { name = "Kinkaku-ji" });
        var delete = await client.DeleteAsync($"{Route(_kyoto.Id)}/{GionId()}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }
}
