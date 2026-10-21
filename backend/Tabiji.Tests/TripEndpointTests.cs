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
    private static readonly string[] ExpectedListOrder = ["Reykjavík", "Rome", "Kyoto"];

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
    public async Task List_ReturnsTheDestinationsOfEveryTripInOrder()
    {
        using var client = _factory.CreateClient();

        var trips = await client.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.NotNull(trips);

        var rome = trips.Single(trip => trip.Name == "Rome");

        Assert.Equal(["Rome", "Amalfi", "Naples"], rome.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2], rome.Destinations.Select(d => d.SortOrder));
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

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;

        var trip = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(trip);
        Assert.Equal("Rome", trip.Name);
        Assert.Equal(["Rome", "Amalfi", "Naples"], trip.Destinations.Select(d => d.Name));
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
            "Lisbon",
            ["Lisbon", "Porto", "Faro"],
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 3));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(payload);

        var created = payload!;
        Assert.Equal("Lisbon", created.Name);
        Assert.Equal(["Lisbon", "Porto", "Faro"], created.Destinations.Select(d => d.Name));
        Assert.Equal(TripStatus.Planning, created.Status);

        var trips = await client.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.NotNull(trips);
        Assert.Contains(trips, trip => trip.Id == created.Id);
    }

    [Fact]
    public async Task Create_AcceptsAnInternationalDestination()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Rome & Amalfi",
            ["Rome, Italy", "Amalfi, Italy"],
            new DateOnly(2027, 6, 1),
            new DateOnly(2027, 6, 12));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(payload);

        Assert.Equal("Rome & Amalfi", payload!.Name);
        Assert.Equal(["Rome, Italy", "Amalfi, Italy"], payload.Destinations.Select(d => d.Name));
    }

    [Theory]
    [InlineData("   ", "2027-05-01", "2027-05-03")]
    [InlineData("Lisbon", "2027-05-03", "2027-05-01")]
    public async Task Create_ReturnsBadRequestForAnInvalidTrip(string name, string startsOn, string endsOn)
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            name,
            ["Lisbon", "Porto"],
            DateOnly.Parse(startsOn, CultureInfo.InvariantCulture),
            DateOnly.Parse(endsOn, CultureInfo.InvariantCulture));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequestWhenThereAreNoDestinations()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Lisbon",
            [],
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 3));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequestWhenADestinationIsBlank()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Lisbon",
            ["Lisbon", "   "],
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 3));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequestWhenThereAreTooManyDestinations()
    {
        using var client = _factory.CreateClient();

        var destinations = Enumerable.Range(1, Trip.MaxDestinationCount + 1)
            .Select(index => $"Stop {index}")
            .ToArray();

        var request = new CreateTripRequest(
            "Too many stops",
            destinations,
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 3));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
