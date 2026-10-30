using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tabiji.Application.Trips;

namespace Tabiji.Tests;

/// <summary>
/// Who may reach a trip.
///
/// The trip ids are real ids of trips the owner really has, so these are not
/// tests about a missing row: they are tests about a row that exists and is
/// answered as though it did not. A trip the caller is not a member of and a
/// trip that does not exist are the same answer, on every route.
/// </summary>
public sealed class TripAuthorisationEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TripApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("GET", "/api/trips")]
    [InlineData("POST", "/api/trips")]
    [InlineData("GET", "/api/trips/{id}")]
    [InlineData("PUT", "/api/trips/{id}")]
    [InlineData("DELETE", "/api/trips/{id}")]
    [InlineData("GET", "/api/trips/{id}/places")]
    [InlineData("GET", "/api/trips/{id}/places/{placeId}")]
    public async Task AnyTripRoute_AnswersUnauthorizedWithoutAnAccount(
        string method,
        string template)
    {
        using var client = _factory.CreateAnonymousClient();

        var route = template
            .Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal)
            .Replace("{placeId}", Guid.NewGuid().ToString(), StringComparison.Ordinal);

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheTripsTheAccountIsAMemberOf()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var mine = await owner.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);
        var theirs = await other.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.Equal(3, mine!.Length);
        Assert.Empty(theirs!);
    }

    [Fact]
    public async Task AnotherAccountsTrip_IsAnsweredExactlyAsAnUnknownOne()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var id = await RomeIdAsync(owner);

        var foreignResponse = await other.GetAsync($"/api/trips/{id}");
        var unknownResponse = await other.GetAsync($"/api/trips/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);

        var foreign = await foreignResponse.Content.ReadFromJsonAsync<Problem>(JsonOptions);
        var unknown = await unknownResponse.Content.ReadFromJsonAsync<Problem>(JsonOptions);

        Assert.NotNull(foreign);
        Assert.NotNull(unknown);

        // The same title, the same absence of detail, the same status: an id
        // cannot be used to learn that somebody else's trip exists.
        Assert.Equal("Trip not found", foreign.Title);
        Assert.Equal(unknown.Title, foreign.Title);
        Assert.Equal(unknown.Detail, foreign.Detail);
        Assert.Equal(unknown.Status, foreign.Status);
    }

    [Fact]
    public async Task AnotherAccountsPlaceRoutes_AreAnsweredAsATripThatDoesNotExist()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var id = await RomeIdAsync(owner);

        var places = await other.GetAsync($"/api/trips/{id}/places");
        var place = await other.GetAsync($"/api/trips/{id}/places/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, places.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, place.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDeleteOfAnotherAccountsTrip_ChangeNothing()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var trip = await RomeAsync(owner);

        var update = await other.PutAsJsonAsync(
            $"/api/trips/{trip.Id}",
            new UpdateTripRequest(
                "Stolen",
                [.. trip.Destinations.Select(destination =>
                    new TripDestinationUpdateRequest(destination.Id, destination.Name, destination.StartsOn, destination.EndsOn))],
                trip.StartsOn,
                trip.EndsOn),
            JsonOptions);

        var delete = await other.DeleteAsync($"/api/trips/{trip.Id}");

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        var stored = await owner.GetFromJsonAsync<TripResponse>($"/api/trips/{trip.Id}", JsonOptions);

        Assert.NotNull(stored);
        Assert.Equal("Rome", stored.Name);
    }

    [Fact]
    public async Task AnOwnerIdInTheBody_IsIgnored()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var response = await owner.PostAsJsonAsync(
            "/api/trips",
            new
            {
                name = "Japan",
                startsOn = "2027-05-01",
                endsOn = "2027-05-02",
                destinations = new[] { new { name = "Osaka" } },
                ownerId = TestUsers.OtherId,
                members = new[] { new { userId = TestUsers.OtherId, role = "Owner" } }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.NotNull(created);

        // The caller is the owner, whatever the body said.
        Assert.Empty(await other.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions) ?? []);
        Assert.NotNull(await owner.GetFromJsonAsync<TripResponse>($"/api/trips/{created.Id}", JsonOptions));
    }

    private static async Task<Guid> RomeIdAsync(HttpClient client) => (await RomeAsync(client)).Id;

    private static async Task<TripResponse> RomeAsync(HttpClient client)
    {
        var trips = await client.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.NotNull(trips);

        return trips.Single(trip => trip.Name == "Rome");
    }

    private sealed record Problem(string? Title, string? Detail, int? Status);
}
