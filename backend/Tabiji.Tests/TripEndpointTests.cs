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
    public async Task List_ReturnsTheDatesOfEveryDestination()
    {
        using var client = _factory.CreateClient();

        var trips = await client.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.NotNull(trips);

        var rome = trips.Single(trip => trip.Name == "Rome");

        Assert.Equal(
            [new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 4), new DateOnly(2027, 4, 7)],
            rome.Destinations.Select(d => d.StartsOn));
        Assert.Equal(
            [new DateOnly(2027, 4, 4), new DateOnly(2027, 4, 6), new DateOnly(2027, 4, 8)],
            rome.Destinations.Select(d => d.EndsOn));
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
    public async Task GetById_ReturnsTheIdentityAndTheDatesOfEveryDestination()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;

        var trip = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(trip);
        Assert.All(trip.Destinations, destination => Assert.NotEqual(Guid.Empty, destination.Id));
        Assert.Equal(
            new DateOnly(2027, 4, 1),
            trip.Destinations[0].StartsOn);
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
            [new("Lisbon", null, null), new("Porto", null, null), new("Faro", null, null)],
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
    public async Task Create_KeepsTheDatesAndTheOrderOfTheDestinations()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Japan Adventure",
            [
                new("Osaka", new DateOnly(2027, 5, 16), new DateOnly(2027, 5, 20)),
                new("Kyoto", new DateOnly(2027, 5, 20), new DateOnly(2027, 5, 24)),
                new("Ise", null, null),
                new("Tokyo", new DateOnly(2027, 5, 26), new DateOnly(2027, 6, 5))
            ],
            new DateOnly(2027, 5, 16),
            new DateOnly(2027, 6, 5));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(payload);

        var destinations = payload!.Destinations;

        Assert.Equal(["Osaka", "Kyoto", "Ise", "Tokyo"], destinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2, 3], destinations.Select(d => d.SortOrder));
        Assert.All(destinations, destination => Assert.NotEqual(Guid.Empty, destination.Id));
        Assert.Equal(new DateOnly(2027, 5, 16), destinations[0].StartsOn);
        Assert.Equal(new DateOnly(2027, 5, 20), destinations[0].EndsOn);
        Assert.Null(destinations[2].StartsOn);
        Assert.Null(destinations[2].EndsOn);
    }

    [Fact]
    public async Task Create_AcceptsAnInternationalDestination()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Rome & Amalfi",
            [new("Rome, Italy", null, null), new("Amalfi, Italy", null, null)],
            new DateOnly(2027, 6, 1),
            new DateOnly(2027, 6, 12));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(payload);

        Assert.Equal("Rome & Amalfi", payload!.Name);
        Assert.Equal(["Rome, Italy", "Amalfi, Italy"], payload.Destinations.Select(d => d.Name));
    }

    [Fact]
    public async Task Create_KeepsTheCoordinatesOfAPlaceChosenFromAMap()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Japan Adventure",
            [new("Kyoto", null, null, 35.0116, 135.7681), new("Osaka", null, null)],
            new DateOnly(2027, 5, 16),
            new DateOnly(2027, 5, 20));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(payload);

        Assert.Equal(35.0116, payload!.Destinations[0].Latitude);
        Assert.Equal(135.7681, payload.Destinations[0].Longitude);

        // A place typed by hand still has no coordinates, and that is valid.
        Assert.Null(payload.Destinations[1].Latitude);
        Assert.Null(payload.Destinations[1].Longitude);
    }

    [Theory]
    [InlineData(35.0116, null)]
    [InlineData(null, 135.7681)]
    [InlineData(91.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(0.0, -181.0)]
    public async Task Create_ReturnsBadRequestForIncompleteOrImpossibleCoordinates(
        double? latitude,
        double? longitude)
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Japan Adventure",
            [new("Kyoto", null, null, latitude, longitude)],
            new DateOnly(2027, 5, 16),
            new DateOnly(2027, 5, 20));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("   ", "2027-05-01", "2027-05-03")]
    [InlineData("Lisbon", "2027-05-03", "2027-05-01")]
    public async Task Create_ReturnsBadRequestForAnInvalidTrip(string name, string startsOn, string endsOn)
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            name,
            [new("Lisbon", null, null), new("Porto", null, null)],
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
            [new("Lisbon", null, null), new("   ", null, null)],
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
            .Select(index => new TripDestinationRequest($"Stop {index}", null, null))
            .ToArray();

        var request = new CreateTripRequest(
            "Too many stops",
            destinations,
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 3));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequestWhenADateFallsOutsideTheTrip()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Japan Adventure",
            [new("Osaka", new DateOnly(2027, 5, 16), new DateOnly(2027, 5, 20)),
             new("Tokyo", new DateOnly(2027, 5, 26), new DateOnly(2027, 6, 10))],
            new DateOnly(2027, 5, 16),
            new DateOnly(2027, 6, 5));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Contains("Tokyo", problem!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_ReturnsBadRequestWhenTheDatesContradictTheOrder()
    {
        using var client = _factory.CreateClient();

        var request = new CreateTripRequest(
            "Japan Adventure",
            [new("Osaka", new DateOnly(2027, 5, 16), new DateOnly(2027, 5, 20)),
             new("Kyoto", new DateOnly(2027, 5, 18), new DateOnly(2027, 5, 22))],
            new DateOnly(2027, 5, 16),
            new DateOnly(2027, 6, 5));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Contains("Osaka", problem!.Detail, StringComparison.Ordinal);
        Assert.Contains("Kyoto", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_ChangesTheTripAndItsRoute()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;
        var stored = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);
        Assert.NotNull(stored);

        var rome = stored!.Destinations[0].Id;
        var naples = stored.Destinations[2].Id;

        var request = new UpdateTripRequest(
            "Italia",
            [
                new(naples, "Napoli", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 4)),
                new(rome, "Roma", new DateOnly(2027, 4, 4), new DateOnly(2027, 4, 7)),
                new(null, "Pompeii", null, null)
            ],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8));

        var response = await client.PutAsJsonAsync($"/api/trips/{id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(payload);

        var updated = payload!;

        Assert.Equal("Italia", updated.Name);
        Assert.Equal(["Napoli", "Roma", "Pompeii"], updated.Destinations.Select(d => d.Name));
        Assert.Equal([0, 1, 2], updated.Destinations.Select(d => d.SortOrder));
        Assert.Equal(naples, updated.Destinations[0].Id);
        Assert.Equal(rome, updated.Destinations[1].Id);
        Assert.Equal(new DateOnly(2027, 4, 4), updated.Destinations[1].StartsOn);
        Assert.Null(updated.Destinations[2].StartsOn);
    }

    [Fact]
    public async Task Update_RefusesToReorderStopsWhoseDatesContradictTheNewOrder()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;
        var stored = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);
        Assert.NotNull(stored);

        var rome = stored!.Destinations[0].Id;
        var naples = stored.Destinations[2].Id;

        // Rome is 1–4 April and Naples is 7–8 April. Swapping them puts the
        // earlier stop second, which the order of the route forbids. The
        // route is not silently put right: the request is refused.
        var request = new UpdateTripRequest(
            "Rome",
            [
                new(naples, "Naples", new DateOnly(2027, 4, 7), new DateOnly(2027, 4, 8)),
                new(rome, "Rome", new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 4))
            ],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8));

        var response = await client.PutAsJsonAsync($"/api/trips/{id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Contains("Rome", problem!.Detail, StringComparison.Ordinal);
        Assert.Contains("Naples", problem.Detail, StringComparison.Ordinal);

        var reread = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(reread);
        Assert.Equal(["Rome", "Amalfi", "Naples"], reread!.Destinations.Select(d => d.Name));
    }

    [Fact]
    public async Task Update_ClearsADateWhenItIsSentAsNull()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;
        var stored = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);
        Assert.NotNull(stored);

        var rome = stored!.Destinations[0].Id;

        var request = new UpdateTripRequest(
            "Rome",
            [new(rome, "Rome", null, null)],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8));

        var response = await client.PutAsJsonAsync($"/api/trips/{id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(payload);

        Assert.Null(payload!.Destinations[0].StartsOn);
        Assert.Null(payload.Destinations[0].EndsOn);

        var reread = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(reread);
        Assert.Null(reread!.Destinations[0].StartsOn);
    }

    [Fact]
    public async Task Update_CanSetChangeAndClearCoordinates()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;
        var stored = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);
        Assert.NotNull(stored);

        var rome = stored!.Destinations[0].Id;

        // A place chosen from a map carries its coordinates.
        var setResponse = await client.PutAsJsonAsync($"/api/trips/{id}", new UpdateTripRequest(
            "Rome",
            [new(rome, "Rome", null, null, 41.9028, 12.4964)],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8)));

        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);

        var set = await setResponse.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(set);
        Assert.Equal(41.9028, set!.Destinations[0].Latitude);
        Assert.Equal(12.4964, set.Destinations[0].Longitude);

        // Replacing the name by hand clears them, so a place is never a name
        // from one place with the coordinates of another.
        var clearResponse = await client.PutAsJsonAsync($"/api/trips/{id}", new UpdateTripRequest(
            "Rome",
            [new(rome, "Rome, Italy", null, null)],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8)));

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);

        var reread = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(reread);
        Assert.Null(reread!.Destinations[0].Latitude);
        Assert.Null(reread.Destinations[0].Longitude);
    }

    [Fact]
    public async Task Update_ReturnsBadRequestForAHalfSetCoordinatePairAndLeavesTheTripUnchanged()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;
        var stored = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);
        Assert.NotNull(stored);

        var rome = stored!.Destinations[0].Id;

        var response = await client.PutAsJsonAsync($"/api/trips/{id}", new UpdateTripRequest(
            "Rome",
            [new(rome, "Rome", null, null, 41.9028, null)],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var reread = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(reread);
        Assert.Null(reread!.Destinations[0].Latitude);
        Assert.Null(reread.Destinations[0].Longitude);
    }

    [Fact]
    public async Task Update_KeepsTheIdentityOfAStopThatStays()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;
        var stored = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);
        Assert.NotNull(stored);

        var rome = stored!.Destinations[0].Id;

        var request = new UpdateTripRequest(
            "Rome",
            [new(rome, "Roma", null, null)],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8));

        await client.PutAsJsonAsync($"/api/trips/{id}", request);

        var reread = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(reread);
        Assert.Equal(rome, reread!.Destinations[0].Id);
        Assert.Equal("Roma", reread.Destinations[0].Name);
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForAnUnknownTrip()
    {
        using var client = _factory.CreateClient();

        var request = new UpdateTripRequest(
            "Rome",
            [new(null, "Rome", null, null)],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8));

        var response = await client.PutAsJsonAsync($"/api/trips/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsBadRequestAndLeavesTheTripUnchangedWhenTheRouteIsRefused()
    {
        using var client = _factory.CreateClient();

        var id = (await _factory.Repository.ListAsync()).First(trip => trip.Name == "Rome").Id;
        var stored = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);
        Assert.NotNull(stored);

        var rome = stored!.Destinations[0].Id;

        var request = new UpdateTripRequest(
            "Changed",
            [new(rome, "Roma", new DateOnly(2027, 3, 30), null)],
            new DateOnly(2027, 4, 1),
            new DateOnly(2027, 4, 8));

        var response = await client.PutAsJsonAsync($"/api/trips/{id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var reread = await client.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(reread);
        Assert.Equal("Rome", reread!.Name);
        Assert.Equal(["Rome", "Amalfi", "Naples"], reread.Destinations.Select(d => d.Name));
        Assert.Equal(new DateOnly(2027, 4, 1), reread.Destinations[0].StartsOn);
    }

    [Fact]
    public async Task Update_ReturnsBadRequestWhenADestinationBelongsToAnotherTrip()
    {
        using var client = _factory.CreateClient();

        var trips = await _factory.Repository.ListAsync();
        var rome = trips.First(trip => trip.Name == "Rome");
        var kyoto = trips.First(trip => trip.Name == "Kyoto");

        var request = new UpdateTripRequest(
            "Kyoto",
            [new(rome.Destinations[0].Id, "Osaka", null, null)],
            kyoto.StartsOn,
            kyoto.EndsOn);

        var response = await client.PutAsJsonAsync($"/api/trips/{kyoto.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record ProblemDetails(string? Title, string? Detail, int? Status);
}
