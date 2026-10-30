using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tabiji.Application.Trips;

namespace Tabiji.Tests;

/// <summary>
/// The antiforgery token on the routes that change a trip.
///
/// The cookie is sent by the browser on its own, so a state-changing request
/// has to prove it came from the page that holds it. The Angular client
/// already stamps every unsafe request, so this is the shape it depends on.
/// </summary>
public sealed class AntiforgeryOnTripEndpointsTests : IDisposable
{
    private const string Header = "X-XSRF-TOKEN";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TripApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Create_WithoutTheToken_IsRefused()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove(Header);

        var response = await client.PostAsJsonAsync("/api/trips", NewTrip());

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task Create_WithAWrongToken_IsRefused()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove(Header);
        client.DefaultRequestHeaders.Add(Header, "not-a-real-token");

        var response = await client.PostAsJsonAsync("/api/trips", NewTrip());

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task Update_WithoutTheToken_IsRefused()
    {
        var id = await RomeIdAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove(Header);

        var response = await client.PutAsJsonAsync(
            $"/api/trips/{id}",
            new UpdateTripRequest("Stolen", [], new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 2)));

        await AssertInvalidTokenAsync(response);
    }

    [Fact]
    public async Task Delete_WithoutTheToken_IsRefused()
    {
        var id = await RomeIdAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove(Header);

        var response = await client.DeleteAsync($"/api/trips/{id}");

        await AssertInvalidTokenAsync(response);

        // Refused means refused: the trip is still there.
        using var owner = _factory.CreateClient();
        var trip = await owner.GetFromJsonAsync<TripResponse>($"/api/trips/{id}", JsonOptions);

        Assert.NotNull(trip);
    }

    [Fact]
    public async Task Reading_NeedsNoToken()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove(Header);

        var response = await client.GetAsync("/api/trips");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithTheToken_Succeeds()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/trips", NewTrip());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static object NewTrip() => new
    {
        name = "Japan",
        startsOn = "2027-05-01",
        endsOn = "2027-05-02",
        destinations = new[] { new { name = "Osaka" } }
    };

    private async Task<Guid> RomeIdAsync()
    {
        using var client = _factory.CreateClient();

        var trips = await client.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions);

        Assert.NotNull(trips);

        return trips.Single(trip => trip.Name == "Rome").Id;
    }

    private static async Task AssertInvalidTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<Problem>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal("Invalid antiforgery token", problem.Title);
    }

    private sealed record Problem(string? Title, string? Detail, int? Status);
}
