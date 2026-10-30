using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tabiji.Api.Endpoints;
using Tabiji.Application.Trips;
using Tabiji.Infrastructure.Persistence;

namespace Tabiji.Tests;

/// <summary>
/// Two real accounts, through the real cookie, against a real database.
///
/// The endpoint tests use a header instead of the cookie and a fake repository;
/// this is the one that proves the whole path: Identity issues the cookie, the
/// antiforgery filter accepts the token, the repository scopes the query by the
/// membership row, and the other account is answered as though the trip did
/// not exist.
/// </summary>
public sealed class TripOwnershipIntegrationTests : IAsyncLifetime, IDisposable
{
    private const string Password = "CorrectHorse42";
    private const string OwnerEmail = "owner@example.com";
    private const string OtherEmail = "other@example.com";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly OwnershipApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var context = new TabijiDbContext(Options());

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        SqlConnection.ClearAllPools();

        await using var context = new TabijiDbContext(Options());

        await context.Database.EnsureDeletedAsync();

        Dispose();
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [DatabaseFact]
    public async Task AnotherAccount_CanNeitherSeeNorChangeTheTrip()
    {
        using var owner = _factory.CreateClient();
        using var other = _factory.CreateClient();

        await SignInAsync(owner, OwnerEmail);
        await SignInAsync(other, OtherEmail);

        var created = await CreateJapanAsync(owner);

        // The owner sees it; the other account sees nothing at all.
        Assert.NotNull(await owner.GetFromJsonAsync<TripResponse>($"/api/trips/{created.Id}", JsonOptions));
        Assert.Empty(await other.GetFromJsonAsync<TripResponse[]>("/api/trips", JsonOptions) ?? []);

        var read = await other.GetAsync($"/api/trips/{created.Id}");
        var update = await other.PutAsJsonAsync(
            $"/api/trips/{created.Id}",
            new UpdateTripRequest("Stolen", [], created.StartsOn, created.EndsOn),
            JsonOptions);
        var delete = await other.DeleteAsync($"/api/trips/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        // Nothing was changed by the attempts.
        var stored = await owner.GetFromJsonAsync<TripResponse>($"/api/trips/{created.Id}", JsonOptions);

        Assert.NotNull(stored);
        Assert.Equal("Japan", stored.Name);
        Assert.Single(stored.Destinations);
    }

    [DatabaseFact]
    public async Task TheOwner_CanChangeAndDeleteTheTrip()
    {
        using var owner = _factory.CreateClient();

        await SignInAsync(owner, OwnerEmail);

        var created = await CreateJapanAsync(owner);

        var update = await owner.PutAsJsonAsync(
            $"/api/trips/{created.Id}",
            new UpdateTripRequest(
                "Japan 2027",
                [.. created.Destinations.Select(destination =>
                    new TripDestinationUpdateRequest(
                        destination.Id,
                        "Osaka",
                        destination.StartsOn,
                        destination.EndsOn))],
                created.StartsOn,
                created.EndsOn),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var renamed = await update.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);
        Assert.Equal("Japan 2027", renamed!.Name);

        var delete = await owner.DeleteAsync($"/api/trips/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await owner.GetAsync($"/api/trips/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [DatabaseFact]
    public async Task ReadingWithoutTheCookie_IsRefused()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/trips");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [DatabaseFact]
    public async Task WritingWithoutTheAntiforgeryToken_IsRefusedEvenWithACookie()
    {
        using var client = _factory.CreateClient();

        await SignInAsync(client, OwnerEmail);
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        var response = await client.PostAsJsonAsync("/api/trips", NewTrip("Japan"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static DbContextOptions<TabijiDbContext> Options() =>
        new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(OwnershipApiFactory.ConnectionString)
            .Options;

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var credentials = new Credentials(email, Password);

        var register = await client.PostAsJsonAsync("/api/auth/register", credentials);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", credentials);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // The token embeds the account, so it is collected after signing in.
        var csrf = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf", JsonOptions);

        Assert.NotNull(csrf);
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf.Token);
    }

    private static async Task<TripResponse> CreateJapanAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/trips", NewTrip("Japan"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var trip = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);

        Assert.NotNull(trip);

        return trip;
    }

    private static object NewTrip(string name) => new
    {
        name,
        startsOn = "2027-05-01",
        endsOn = "2027-05-05",
        destinations = new[]
        {
            new
            {
                name = "Osaka",
                startsOn = "2027-05-01",
                endsOn = "2027-05-05",
                stops = new[] { new { name = "Namba", latitude = 34.6660, longitude = 135.5000 } }
            }
        }
    };

    private sealed record Credentials(string Email, string Password);
}
