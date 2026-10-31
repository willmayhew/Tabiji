using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Tabiji.Api.Endpoints;
using Tabiji.Application.Membership;
using Tabiji.Application.Trips;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;
using Tabiji.Infrastructure.Persistence;
using Tabiji.Infrastructure.Security;

namespace Tabiji.Tests;

/// <summary>
/// Collaboration through the real API, the real repositories and a disposable
/// database.
///
/// The accounts are seeded with the fixed test ids and header authentication
/// stands in for the cookie, so a test can be the owner, the invitee or a
/// stranger without registering anybody. Everything below the authentication
/// handler is the shipping code.
/// </summary>
public sealed class TripCollaborationApiTests : IAsyncLifetime, IDisposable
{
    private const string AntiforgeryHeader = "X-XSRF-TOKEN";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CollaborationApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var context = NewContext();

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        await SeedAccountAsync(TestUsers.OwnerId, "owner@example.com");
        await SeedAccountAsync(TestUsers.OtherId, "other@example.com");
        await SeedAccountAsync(TestUsers.StrangerId, "stranger@example.com");
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        SqlConnection.ClearAllPools();

        await using var context = NewContext();

        await context.Database.EnsureDeletedAsync();

        Dispose();
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [DatabaseFact]
    public async Task OwnerSeesMembersAndTheInvitationCarriesADevelopmentLink()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);

        var trip = await CreateTripAsync(owner);

        Assert.Equal(TripMemberRole.Owner, trip.ViewerRole);

        var members = await owner.GetFromJsonAsync<TripMembersResponse>(
            $"/api/trips/{trip.Id}/members",
            JsonOptions);

        Assert.NotNull(members);
        Assert.Equal(TripMemberRole.Owner, members.ViewerRole);

        var member = Assert.Single(members.Members);

        Assert.Equal(TestUsers.OwnerId, member.UserId);
        Assert.Equal("owner@example.com", member.Email);
        Assert.Equal(TripMemberRole.Owner, member.Role);

        var invitation = await InviteAsync(owner, trip.Id, "other@example.com");

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.NotNull(invitation.InvitationUrl);
        Assert.Contains("/invite#token=", invitation.InvitationUrl, StringComparison.Ordinal);

        var list = await owner.GetFromJsonAsync<TripInvitationResponse[]>(
            $"/api/trips/{trip.Id}/invitations",
            JsonOptions);

        Assert.NotNull(list);
        Assert.Single(list);
    }

    [DatabaseFact]
    public async Task CollaborationReadsAreNotStored()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);

        var trip = await CreateTripAsync(owner);

        var tripResponse = await owner.GetAsync($"/api/trips/{trip.Id}");
        var membersResponse = await owner.GetAsync($"/api/trips/{trip.Id}/members");

        // A member list and a trip are private to the account that asked for
        // them, so no cache along the way may keep a copy.
        Assert.Contains(
            "no-store",
            tripResponse.Headers.CacheControl?.ToString() ?? string.Empty,
            StringComparison.Ordinal);

        Assert.Contains(
            "no-store",
            membersResponse.Headers.CacheControl?.ToString() ?? string.Empty,
            StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task InviteeAcceptsAndBecomesAnEditorWhoMayEditButNotManage()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var trip = await CreateTripAsync(owner);
        var invitation = await InviteAsync(owner, trip.Id, "other@example.com");
        var token = TokenFrom(invitation);

        var preview = await PreviewAsync(other, token);

        Assert.True(preview.CanAccept);
        Assert.Equal("Japan", preview.TripName);

        var accepted = await AcceptAsync(other, token);

        Assert.Equal(TripMemberRole.Editor, accepted.Role);

        var seen = await other.GetFromJsonAsync<TripResponse>(
            $"/api/trips/{trip.Id}",
            JsonOptions);

        Assert.NotNull(seen);
        Assert.Equal(TripMemberRole.Editor, seen.ViewerRole);

        var update = await other.PutAsJsonAsync(
            $"/api/trips/{trip.Id}",
            SameTrip(trip),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await other.GetAsync($"/api/trips/{trip.Id}/members")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await other.GetAsync($"/api/trips/{trip.Id}/invitations")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await other.PostAsJsonAsync(
                $"/api/trips/{trip.Id}/invitations",
                new { email = "stranger@example.com" },
                JsonOptions)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await other.PatchAsJsonAsync(
                $"/api/trips/{trip.Id}/members/{TestUsers.OwnerId}",
                new { role = "Editor" },
                JsonOptions)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await other.DeleteAsync(
                $"/api/trips/{trip.Id}/members/{TestUsers.OwnerId}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await other.DeleteAsync($"/api/trips/{trip.Id}")).StatusCode);

        var mine = await other.GetFromJsonAsync<RecipientInvitationResponse[]>(
            "/api/invitations",
            JsonOptions);

        Assert.NotNull(mine);
        Assert.Empty(mine);
    }

    [DatabaseFact]
    public async Task StrangerCannotReachTheTripOrSeeAnInvitation()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var stranger = _factory.CreateClient(TestUsers.StrangerId);

        var trip = await CreateTripAsync(owner);
        var invitation = await InviteAsync(owner, trip.Id, "other@example.com");
        var token = TokenFrom(invitation);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/trips/{trip.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/trips/{trip.Id}/members")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/trips/{trip.Id}/invitations")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync(
                $"/api/trips/{trip.Id}/invitations",
                new { email = "someone@example.com" },
                JsonOptions)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync(
                "/api/invitations/preview",
                new { token },
                JsonOptions)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync(
                "/api/invitations/accept",
                new { token },
                JsonOptions)).StatusCode);

        var mine = await stranger.GetFromJsonAsync<RecipientInvitationResponse[]>(
            "/api/invitations",
            JsonOptions);

        Assert.NotNull(mine);
        Assert.Empty(mine);
    }

    [DatabaseFact]
    public async Task OwnerRemovesTheEditorAndAccessEndsAtOnce()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var trip = await CreateTripAsync(owner);
        var invitation = await InviteAsync(owner, trip.Id, "other@example.com");

        await AcceptAsync(other, TokenFrom(invitation));

        var removed = await owner.DeleteAsync($"/api/trips/{trip.Id}/members/{TestUsers.OtherId}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/trips/{trip.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/trips/{trip.Id}/members")).StatusCode);
    }

    [DatabaseFact]
    public async Task TheLastOwnerCannotBeRemovedOrDemoted()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);

        var trip = await CreateTripAsync(owner);

        var demote = await owner.PatchAsJsonAsync(
            $"/api/trips/{trip.Id}/members/{TestUsers.OwnerId}",
            new { role = "Editor" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(
            "A trip must keep at least one owner",
            (await demote.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);

        var remove = await owner.DeleteAsync(
            $"/api/trips/{trip.Id}/members/{TestUsers.OwnerId}");

        Assert.Equal(HttpStatusCode.Conflict, remove.StatusCode);

        var members = await owner.GetFromJsonAsync<TripMembersResponse>(
            $"/api/trips/{trip.Id}/members",
            JsonOptions);

        Assert.NotNull(members);
        Assert.Equal(TripMemberRole.Owner, Assert.Single(members.Members).Role);
    }

    [DatabaseFact]
    public async Task InvitationStatesAreReportedToTheRecipient()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);
        using var stranger = _factory.CreateClient(TestUsers.StrangerId);

        // Revoked
        var revokedTrip = await CreateTripAsync(owner, "Revoked trip");
        var revoked = await InviteAsync(owner, revokedTrip.Id, "other@example.com");

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await owner.DeleteAsync(
                $"/api/trips/{revokedTrip.Id}/invitations/{revoked.Id}")).StatusCode);

        var revokedToken = TokenFrom(revoked);
        var revokedPreview = await PreviewAsync(other, revokedToken);

        Assert.Equal(InvitationStatus.Revoked, revokedPreview.Status);
        Assert.False(revokedPreview.CanAccept);

        var revokedAccept = await other.PostAsJsonAsync(
            "/api/invitations/accept",
            new { token = revokedToken },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, revokedAccept.StatusCode);
        Assert.Equal(
            "Invitation was revoked",
            (await revokedAccept.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);

        // Declined
        var declinedTrip = await CreateTripAsync(owner, "Declined trip");
        var declined = await InviteAsync(owner, declinedTrip.Id, "stranger@example.com");
        var declinedToken = TokenFrom(declined);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await stranger.PostAsJsonAsync(
                "/api/invitations/decline",
                new { token = declinedToken },
                JsonOptions)).StatusCode);

        var declinedPreview = await PreviewAsync(stranger, declinedToken);

        Assert.Equal(InvitationStatus.Declined, declinedPreview.Status);

        var declinedAccept = await stranger.PostAsJsonAsync(
            "/api/invitations/accept",
            new { token = declinedToken },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, declinedAccept.StatusCode);
        Assert.Equal(
            "Invitation was declined",
            (await declinedAccept.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);

        // Already used
        var usedTrip = await CreateTripAsync(owner, "Used trip");
        var used = await InviteAsync(owner, usedTrip.Id, "other@example.com");
        var usedToken = TokenFrom(used);

        await AcceptAsync(other, usedToken);

        var usedAccept = await other.PostAsJsonAsync(
            "/api/invitations/accept",
            new { token = usedToken },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, usedAccept.StatusCode);
        Assert.Equal(
            "Invitation has already been used",
            (await usedAccept.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);

        // Expired
        var expiredTrip = await CreateTripAsync(owner, "Expired trip");
        var expiredToken = $"expired-{Guid.NewGuid():N}";

        await SeedExpiredInvitationAsync(expiredTrip.Id, "stranger@example.com", expiredToken);

        var expiredPreview = await PreviewAsync(stranger, expiredToken);

        Assert.Equal(InvitationStatus.Expired, expiredPreview.Status);
        Assert.False(expiredPreview.CanAccept);

        var expiredAccept = await stranger.PostAsJsonAsync(
            "/api/invitations/accept",
            new { token = expiredToken },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, expiredAccept.StatusCode);
        Assert.Equal(
            "Invitation has expired",
            (await expiredAccept.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);
    }

    [DatabaseFact]
    public async Task InvitationCreationDoesNotRevealWhetherAnAccountExists()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);

        var trip = await CreateTripAsync(owner);

        var unregistered = await InviteAsync(owner, trip.Id, "ghost@example.com");
        var registered = await InviteAsync(owner, trip.Id, "other@example.com");

        // The same success, whether or not an account exists: the response is
        // not a way to discover who has registered.
        Assert.Equal(InvitationStatus.Pending, unregistered.Status);
        Assert.Equal(InvitationStatus.Pending, registered.Status);

        var duplicate = await owner.PostAsJsonAsync(
            $"/api/trips/{trip.Id}/invitations",
            new { email = "other@example.com" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(
            "An invitation is already pending for that address",
            (await duplicate.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);

        var member = await owner.PostAsJsonAsync(
            $"/api/trips/{trip.Id}/invitations",
            new { email = "owner@example.com" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, member.StatusCode);
        Assert.Equal(
            "Already a member of this trip",
            (await member.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);
    }

    [DatabaseFact]
    public async Task OwnerPromotesAnEditorToOwnerAndDemotesThemAgain()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var trip = await CreateTripAsync(owner);
        var invitation = await InviteAsync(owner, trip.Id, "other@example.com");

        await AcceptAsync(other, TokenFrom(invitation));

        var promote = await owner.PatchAsJsonAsync(
            $"/api/trips/{trip.Id}/members/{TestUsers.OtherId}",
            new { role = "Owner" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        var promoted = await promote.Content.ReadFromJsonAsync<TripMembersResponse>(JsonOptions);

        Assert.NotNull(promoted);
        Assert.Equal(2, promoted.Members.Count(member => member.Role == TripMemberRole.Owner));
        Assert.Equal(
            HttpStatusCode.OK,
            (await other.GetAsync($"/api/trips/{trip.Id}/invitations")).StatusCode);

        var demote = await owner.PatchAsJsonAsync(
            $"/api/trips/{trip.Id}/members/{TestUsers.OtherId}",
            new { role = "Editor" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, demote.StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await other.GetAsync($"/api/trips/{trip.Id}/invitations")).StatusCode);
    }

    [DatabaseFact]
    public async Task RecipientCanAcceptFromTheTripsPage()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var trip = await CreateTripAsync(owner);

        await InviteAsync(owner, trip.Id, "other@example.com");

        var mine = await other.GetFromJsonAsync<RecipientInvitationResponse[]>(
            "/api/invitations",
            JsonOptions);

        Assert.NotNull(mine);

        var pending = Assert.Single(mine);

        Assert.Equal(trip.Id, pending.TripId);
        Assert.Equal("Japan", pending.TripName);

        var accept = await other.PostAsync(
            $"/api/invitations/{pending.Id}/accept",
            content: null);

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var accepted = await accept.Content.ReadFromJsonAsync<AcceptedInvitation>(JsonOptions);

        Assert.NotNull(accepted);
        Assert.Equal(TripMemberRole.Editor, accepted.Role);

        var after = await other.GetFromJsonAsync<RecipientInvitationResponse[]>(
            "/api/invitations",
            JsonOptions);

        Assert.NotNull(after);
        Assert.Empty(after);
    }

    [Fact]
    public async Task AnonymousIsRefusedOnEveryCollaborationRoute()
    {
        using var client = _factory.CreateAnonymousClient();

        var tripId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        (HttpMethod Method, string Path, bool Body)[] requests =
        [
            (HttpMethod.Get, $"/api/trips/{tripId}/members", false),
            (HttpMethod.Patch, $"/api/trips/{tripId}/members/{Guid.NewGuid()}", true),
            (HttpMethod.Delete, $"/api/trips/{tripId}/members/{Guid.NewGuid()}", false),
            (HttpMethod.Get, $"/api/trips/{tripId}/invitations", false),
            (HttpMethod.Post, $"/api/trips/{tripId}/invitations", true),
            (HttpMethod.Delete, $"/api/trips/{tripId}/invitations/{invitationId}", false),
            (HttpMethod.Get, "/api/invitations", false),
            (HttpMethod.Post, "/api/invitations/preview", true),
            (HttpMethod.Post, "/api/invitations/accept", true),
            (HttpMethod.Post, "/api/invitations/decline", true),
            (HttpMethod.Post, $"/api/invitations/{invitationId}/accept", true),
            (HttpMethod.Post, $"/api/invitations/{invitationId}/decline", true),
        ];

        foreach (var request in requests)
        {
            using var message = new HttpRequestMessage(request.Method, request.Path);

            if (request.Body)
            {
                message.Content = JsonContent.Create(
                    new { email = "someone@example.com", token = "not-a-token", role = "Editor" });
            }

            var response = await client.SendAsync(message);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [DatabaseFact]
    public async Task CollaborationWritesRequireTheAntiforgeryToken()
    {
        using var owner = _factory.CreateClient(TestUsers.OwnerId);
        using var other = _factory.CreateClient(TestUsers.OtherId);

        var trip = await CreateTripAsync(owner);
        var invitation = await InviteAsync(owner, trip.Id, "other@example.com");
        var token = TokenFrom(invitation);

        owner.DefaultRequestHeaders.Remove(AntiforgeryHeader);
        other.DefaultRequestHeaders.Remove(AntiforgeryHeader);

        var create = await owner.PostAsJsonAsync(
            $"/api/trips/{trip.Id}/invitations",
            new { email = "stranger@example.com" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(
            "Invalid antiforgery token",
            (await create.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await owner.PatchAsJsonAsync(
                $"/api/trips/{trip.Id}/members/{TestUsers.OwnerId}",
                new { role = "Owner" },
                JsonOptions)).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await owner.DeleteAsync(
                $"/api/trips/{trip.Id}/members/{TestUsers.OtherId}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await other.PostAsJsonAsync(
                "/api/invitations/accept",
                new { token },
                JsonOptions)).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await other.PostAsJsonAsync(
                "/api/invitations/decline",
                new { token },
                JsonOptions)).StatusCode);
    }

    [Fact]
    public async Task ProductionWithoutEmailRefusesInvitationCreation()
    {
        using var factory = new TripApiFactory(Environments.Production);
        using var owner = factory.CreateClient(TestUsers.OwnerId, new Uri("https://localhost"));

        var response = await owner.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid()}/invitations",
            new { email = "other@example.com" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            "Email delivery is not configured",
            (await response.Content.ReadFromJsonAsync<Problem>(JsonOptions))?.Title);

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("invitationUrl", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    private static TabijiDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(CollaborationApiFactory.ConnectionString)
            .Options;

        return new TabijiDbContext(options);
    }

    private static async Task<TripResponse> CreateTripAsync(
        HttpClient client,
        string name = "Japan")
    {
        var response = await client.PostAsJsonAsync(
            "/api/trips",
            new
            {
                name,
                startsOn = "2027-05-01",
                endsOn = "2027-05-05",
                destinations = new[]
                {
                    new { name = "Osaka", startsOn = "2027-05-01", endsOn = "2027-05-05" },
                },
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var trip = await response.Content.ReadFromJsonAsync<TripResponse>(JsonOptions);

        Assert.NotNull(trip);

        return trip;
    }

    private static async Task<InvitationResponse> InviteAsync(
        HttpClient owner,
        Guid tripId,
        string email)
    {
        var response = await owner.PostAsJsonAsync(
            $"/api/trips/{tripId}/invitations",
            new { email },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var invitation = await response.Content.ReadFromJsonAsync<InvitationResponse>(JsonOptions);

        Assert.NotNull(invitation);

        return invitation;
    }

    private static async Task<InvitationPreviewResponse> PreviewAsync(
        HttpClient recipient,
        string token)
    {
        var response = await recipient.PostAsJsonAsync(
            "/api/invitations/preview",
            new { token },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.Content.ReadFromJsonAsync<InvitationPreviewResponse>(JsonOptions);

        Assert.NotNull(preview);

        return preview;
    }

    private static async Task<AcceptedInvitation> AcceptAsync(
        HttpClient recipient,
        string token)
    {
        var response = await recipient.PostAsJsonAsync(
            "/api/invitations/accept",
            new { token },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var accepted = await response.Content.ReadFromJsonAsync<AcceptedInvitation>(JsonOptions);

        Assert.NotNull(accepted);

        return accepted;
    }

    private static UpdateTripRequest SameTrip(TripResponse trip) => new(
        trip.Name,
        [
            .. trip.Destinations.Select(destination => new TripDestinationUpdateRequest(
                destination.Id,
                destination.Name,
                destination.StartsOn,
                destination.EndsOn)),
        ],
        trip.StartsOn,
        trip.EndsOn);

    private static string TokenFrom(InvitationResponse invitation)
    {
        Assert.NotNull(invitation.InvitationUrl);

        const string marker = "#token=";
        var index = invitation.InvitationUrl.IndexOf(marker, StringComparison.Ordinal);

        Assert.True(index >= 0, "The development invitation URL must carry a token fragment.");

        return Uri.UnescapeDataString(invitation.InvitationUrl[(index + marker.Length)..]);
    }

    private static async Task SeedAccountAsync(Guid userId, string email)
    {
        await using var connection = new SqlConnection(CollaborationApiFactory.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [AspNetUsers]
                ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail],
                 [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp],
                 [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled],
                 [LockoutEnd], [LockoutEnabled], [AccessFailedCount])
            VALUES
                (@id, @email, UPPER(@email), @email, UPPER(@email),
                 1, 'hash', 'stamp', 'stamp', NULL, 0, 0, NULL, 1, 0);
            """;

        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@email", email);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedExpiredInvitationAsync(
        Guid tripId,
        string email,
        string token)
    {
        var hash = new InvitationTokens().Hash(token);
        var now = DateTimeOffset.UtcNow;

        await using var connection = new SqlConnection(CollaborationApiFactory.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [Invitations]
                ([Id], [TripId], [Email], [NormalizedEmail], [Role], [TokenHash], [Status],
                 [CreatedAt], [ExpiresAt], [InvitedByUserId])
            VALUES
                (@id, @trip, @email, UPPER(@email), 'Editor', @hash, 'Pending',
                 @createdAt, @expiresAt, @invitedBy);
            """;

        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@trip", tripId);
        command.Parameters.AddWithValue("@email", email);
        command.Parameters.AddWithValue("@hash", hash);
        command.Parameters.AddWithValue("@createdAt", now.AddDays(-2));
        command.Parameters.AddWithValue("@expiresAt", now.AddDays(-1));
        command.Parameters.AddWithValue("@invitedBy", TestUsers.OwnerId);

        await command.ExecuteNonQueryAsync();
    }

    private sealed record Problem(string? Title, string? Detail, int? Status);
}
