using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tabiji.Application.Trips;
using Tabiji.Domain.Trips;

namespace Tabiji.Tests;

public sealed class TripEndpointTests : IDisposable
{
    private static readonly string[] ExpectedListOrder = ["Hakone", "Tokyo", "Kyoto"];

    // The API serialises enums as strings, so the test client needs the same options.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TripApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task List_ReturnsTheSeededTripsOrderedByStartDateThenName()
    {
        using var client = _factory.CreateClient();

        var trips = await client.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.NotNull(trips);
        Assert.Equal(ExpectedListOrder, trips.Select(trip => trip.Name));
    }

    [Fact]
    public async Task List_ReturnsAnEmptyArrayWhenThereAreNoTrips()
    {
        using var emptyFactory = new TripApiFactory([]);
        using var client = emptyFactory.CreateClient();

        var trips = await client.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.NotNull(trips);
        Assert.Empty(trips);
    }

    [Fact]
    public async Task GetById_ReturnsTheTrip()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Tokyo").Id;

        var trip = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(trip);
        Assert.Equal("Tokyo", trip.Name);
        Assert.Equal("Kanto", trip.Destination);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForAnUnknownId()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/trips/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAndTheTripAppearsInTheList()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Osaka",
            "Kansai",
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 3));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(payload);

        var created = payload!;
        Assert.Equal("Osaka", created.Name);
        Assert.Equal(TripStatus.Planning, created.Status);

        var trips = await client.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.NotNull(trips);
        Assert.Contains(trips, trip => trip.Id == created.Id);
    }

    [Theory]
    [InlineData("   ", "2027-05-01", "2027-05-03")]
    [InlineData("Osaka", "2027-05-03", "2027-05-01")]
    public async Task Create_ReturnsBadRequestForAnInvalidTrip(string name, string startsOn, string endsOn)
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            name,
            "Kansai",
            DateOnly.Parse(startsOn, CultureInfo.InvariantCulture),
            DateOnly.Parse(endsOn, CultureInfo.InvariantCulture));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
