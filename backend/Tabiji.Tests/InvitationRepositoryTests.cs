using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tabiji.Application.Membership;
using Tabiji.Application.Trips;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence;
using Tabiji.Infrastructure.Persistence.Repositories;

namespace Tabiji.Tests;

/// <summary>
/// Membership, invitations and role rules against a real database.
///
/// The fake proves the endpoint wiring; this proves the statements: the
/// filtered unique index, the conditional acceptance, the serializable
/// last-owner check, and the fact that removing a member ends access at once.
/// Each test starts from its own database, so the order they run in does not
/// matter.
/// </summary>
public sealed class InvitationRepositoryTests : IAsyncLifetime, IDisposable
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiInvitationRepositoryTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid EditorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StrangerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static readonly UserId Owner = UserId.Create(OwnerId);
    private static readonly UserId Editor = UserId.Create(EditorId);
    private static readonly UserId Stranger = UserId.Create(StrangerId);

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private TabijiDbContext? _context;
    private TripRepository? _trips;
    private TripMembershipRepository? _members;
    private InvitationRepository? _invitations;
    private Trip? _trip;

    public async Task InitializeAsync()
    {
        SqlConnection.ClearAllPools();

        _context = NewContext();
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.MigrateAsync();

        await SeedAccountAsync(OwnerId, "owner@example.com");
        await SeedAccountAsync(EditorId, "editor@example.com");
        await SeedAccountAsync(StrangerId, "stranger@example.com");

        _trips = new TripRepository(_context);
        _members = new TripMembershipRepository(_context);
        _invitations = new InvitationRepository(_context);

        _trip = Trip.Create(
            "Japan",
            [TripDestinationDraft.Undated("Osaka")],
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 5),
            CreatedAt);

        await _trips.AddAsync(_trip, TripMember.Own(_trip.Id, Owner, CreatedAt));
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.DisposeAsync();
        }

        SqlConnection.ClearAllPools();
        Dispose();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Create_OwnerInvitesAnAddress()
    {
        var result = await InviteAsync("friend@example.com", Hash(1));

        Assert.Equal(InvitationCreateOutcome.Created, result.Outcome);

        var invitation = result.Invitation;

        Assert.NotNull(invitation);
        Assert.Equal(_trip!.Id, invitation.TripId);
        Assert.Equal("friend@example.com", invitation.Email);
        Assert.Equal(TripMemberRole.Editor, invitation.Role);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Equal("owner@example.com", invitation.InvitedByEmail);
    }

    [Fact]
    public async Task Create_EditorIsForbidden()
    {
        await AddEditorAsync();

        var result = await _invitations!.CreateAsync(
            _trip!.Id,
            Editor,
            EmailAddress.Create("friend@example.com"),
            Hash(1),
            CreatedAt,
            CreatedAt.AddDays(7));

        Assert.Equal(InvitationCreateOutcome.Forbidden, result.Outcome);
        Assert.Null(result.Invitation);
    }

    [Fact]
    public async Task Create_NonMemberIsNotFound()
    {
        var result = await _invitations!.CreateAsync(
            _trip!.Id,
            Stranger,
            EmailAddress.Create("friend@example.com"),
            Hash(1),
            CreatedAt,
            CreatedAt.AddDays(7));

        Assert.Equal(InvitationCreateOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Create_RefusesAnAddressThatIsAlreadyAMember()
    {
        await AddEditorAsync();

        var result = await InviteAsync("editor@example.com", Hash(1));

        Assert.Equal(InvitationCreateOutcome.AlreadyMember, result.Outcome);
    }

    [Fact]
    public async Task Create_RefusesADuplicatePendingInvitation()
    {
        await InviteAsync("friend@example.com", Hash(1));

        var result = await InviteAsync("FRIEND@example.com", Hash(2));

        Assert.Equal(InvitationCreateOutcome.AlreadyInvited, result.Outcome);

        var pending = await _context!.Invitations.CountAsync(
            invitation => invitation.TripId == _trip!.Id && invitation.Status == InvitationStatus.Pending);

        Assert.Equal(1, pending);
    }

    [Fact]
    public async Task Create_ReplacesAnExpiredPendingInvitation()
    {
        await _invitations!.CreateAsync(
            _trip!.Id,
            Owner,
            EmailAddress.Create("friend@example.com"),
            Hash(1),
            CreatedAt,
            CreatedAt.AddHours(1));

        var result = await _invitations.CreateAsync(
            _trip.Id,
            Owner,
            EmailAddress.Create("friend@example.com"),
            Hash(2),
            CreatedAt.AddHours(2),
            CreatedAt.AddDays(7));

        Assert.Equal(InvitationCreateOutcome.Created, result.Outcome);

        var statuses = await _context!.Invitations
            .Where(invitation => invitation.TripId == _trip.Id)
            .Select(invitation => invitation.Status)
            .ToListAsync();

        Assert.Equal(
            [InvitationStatus.Pending, InvitationStatus.Expired],
            statuses.OrderBy(status => status));
    }

    [Fact]
    public async Task Preview_IsOnlyForTheNamedAddress()
    {
        await InviteAsync("stranger@example.com", Hash(1));

        var forRecipient = await _invitations!.PreviewAsync(
            Hash(1),
            Stranger,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationPreviewOutcome.Found, forRecipient.Outcome);
        Assert.NotNull(forRecipient.Preview);
        Assert.Equal("Japan", forRecipient.Preview.TripName);
        Assert.Equal("owner@example.com", forRecipient.Preview.InvitedByEmail);
        Assert.Equal(TripMemberRole.Editor, forRecipient.Preview.Role);
        Assert.True(forRecipient.Preview.CanAccept);

        var forSomeoneElse = await _invitations.PreviewAsync(
            Hash(1),
            Owner,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationPreviewOutcome.NotFound, forSomeoneElse.Outcome);
    }

    [Fact]
    public async Task Accept_GrantsTheEditorRoleAndIsSingleUse()
    {
        await InviteAsync("stranger@example.com", Hash(1));

        var accepted = await _invitations!.AcceptAsync(
            Hash(1),
            Stranger,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationAcceptOutcome.Accepted, accepted.Outcome);
        Assert.Equal(TripMemberRole.Editor, accepted.Accepted?.Role);

        var access = await _trips!.GetForMemberAsync(_trip!.Id, Stranger);

        Assert.NotNull(access);
        Assert.Equal(TripMemberRole.Editor, access.Role);

        var again = await _invitations.AcceptAsync(
            Hash(1),
            Stranger,
            CreatedAt.AddHours(2));

        Assert.Equal(InvitationAcceptOutcome.AlreadyUsed, again.Outcome);
        Assert.Equal(1, await CountMembershipAsync(StrangerId));
    }

    [Fact]
    public async Task Accept_ByAnotherAddressIsNotFound()
    {
        await InviteAsync("stranger@example.com", Hash(1));

        var result = await _invitations!.AcceptAsync(
            Hash(1),
            Owner,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationAcceptOutcome.NotFound, result.Outcome);
        Assert.Equal(0, await CountMembershipAsync(StrangerId));
    }

    [Fact]
    public async Task Accept_RefusesAnExpiredInvitation()
    {
        await _invitations!.CreateAsync(
            _trip!.Id,
            Owner,
            EmailAddress.Create("stranger@example.com"),
            Hash(1),
            CreatedAt,
            CreatedAt.AddHours(1));

        var result = await _invitations.AcceptAsync(
            Hash(1),
            Stranger,
            CreatedAt.AddHours(2));

        Assert.Equal(InvitationAcceptOutcome.Expired, result.Outcome);
        Assert.Equal(0, await CountMembershipAsync(StrangerId));

        var status = await _context!.Invitations
            .Where(invitation => invitation.TripId == _trip.Id)
            .Select(invitation => invitation.Status)
            .SingleAsync();

        Assert.Equal(InvitationStatus.Expired, status);
    }

    [Fact]
    public async Task Decline_RecordsTheDecisionAndLeavesNoMembership()
    {
        await InviteAsync("stranger@example.com", Hash(1));

        var result = await _invitations!.DeclineAsync(
            Hash(1),
            Stranger,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationActionOutcome.Changed, result.Outcome);
        Assert.Equal(0, await CountMembershipAsync(StrangerId));

        var status = await _context!.Invitations
            .Select(invitation => invitation.Status)
            .SingleAsync();

        Assert.Equal(InvitationStatus.Declined, status);

        var again = await _invitations.DeclineAsync(
            Hash(1),
            Stranger,
            CreatedAt.AddHours(2));

        Assert.Equal(InvitationActionOutcome.Declined, again.Outcome);
    }

    [Fact]
    public async Task Revoke_IsOwnerOnlyAndStopsAcceptance()
    {
        await AddEditorAsync();
        var invitation = await InviteAsync("stranger@example.com", Hash(1));

        var byEditor = await _invitations!.RevokeAsync(
            _trip!.Id,
            Editor,
            invitation.Invitation!.Id,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationActionOutcome.Forbidden, byEditor.Outcome);

        var byOwner = await _invitations.RevokeAsync(
            _trip.Id,
            Owner,
            invitation.Invitation.Id,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationActionOutcome.Changed, byOwner.Outcome);

        var accept = await _invitations.AcceptAsync(
            Hash(1),
            Stranger,
            CreatedAt.AddHours(2));

        Assert.Equal(InvitationAcceptOutcome.Revoked, accept.Outcome);
        Assert.Equal(0, await CountMembershipAsync(StrangerId));
    }

    [Fact]
    public async Task ListForTrip_IsOwnerOnly()
    {
        await AddEditorAsync();
        await InviteAsync("stranger@example.com", Hash(1));

        var ownerView = await _invitations!.ListForTripAsync(
            _trip!.Id,
            Owner,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationListOutcome.Listed, ownerView.Outcome);
        Assert.Single(ownerView.Invitations);

        var editorView = await _invitations.ListForTripAsync(
            _trip.Id,
            Editor,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationListOutcome.Forbidden, editorView.Outcome);

        var strangerView = await _invitations.ListForTripAsync(
            _trip.Id,
            Stranger,
            CreatedAt.AddHours(1));

        Assert.Equal(InvitationListOutcome.NotFound, strangerView.Outcome);
    }

    [Fact]
    public async Task ListForRecipient_ShowsOnlyTheCallersPendingInvitations()
    {
        await InviteAsync("stranger@example.com", Hash(1));

        var forStranger = await _invitations!.ListForRecipientAsync(
            Stranger,
            CreatedAt.AddHours(1));

        var invitation = Assert.Single(forStranger);

        Assert.Equal(_trip!.Id, invitation.TripId);
        Assert.Equal("Japan", invitation.TripName);
        Assert.Equal("owner@example.com", invitation.InvitedByEmail);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);

        Assert.Empty(await _invitations.ListForRecipientAsync(Owner, CreatedAt.AddHours(1)));

        await _invitations.DeclineAsync(Hash(1), Stranger, CreatedAt.AddHours(2));

        Assert.Empty(await _invitations.ListForRecipientAsync(Stranger, CreatedAt.AddHours(3)));
    }

    [Fact]
    public async Task ChangeRole_PromotesAnEditorToOwnerAndReturnsTheView()
    {
        await AddEditorAsync();

        var result = await _members!.ChangeRoleAsync(
            _trip!.Id,
            Owner,
            Editor,
            TripMemberRole.Owner);

        Assert.Equal(MembershipChangeOutcome.Changed, result.Outcome);
        Assert.NotNull(result.Members);
        Assert.Equal(TripMemberRole.Owner, result.Members.ViewerRole);
        Assert.Equal(2, result.Members.Members.Count(member => member.Role == TripMemberRole.Owner));

        var access = await _trips!.GetForMemberAsync(_trip.Id, Editor);

        Assert.NotNull(access);
        Assert.Equal(TripMemberRole.Owner, access.Role);
    }

    [Fact]
    public async Task ChangeRole_RefusesToDemoteTheLastOwner()
    {
        var result = await _members!.ChangeRoleAsync(
            _trip!.Id,
            Owner,
            Owner,
            TripMemberRole.Editor);

        Assert.Equal(MembershipChangeOutcome.LastOwner, result.Outcome);

        var access = await _trips!.GetForMemberAsync(_trip!.Id, Owner);

        Assert.NotNull(access);
        Assert.Equal(TripMemberRole.Owner, access.Role);
    }

    [Fact]
    public async Task ChangeRole_RefusesAnEditorAsActor()
    {
        await AddEditorAsync();

        var result = await _members!.ChangeRoleAsync(
            _trip!.Id,
            Editor,
            Owner,
            TripMemberRole.Editor);

        Assert.Equal(MembershipChangeOutcome.Forbidden, result.Outcome);
    }

    [Fact]
    public async Task ChangeRole_RefusesAnUnknownRole()
    {
        var result = await _members!.ChangeRoleAsync(
            _trip!.Id,
            Owner,
            Owner,
            (TripMemberRole)99);

        Assert.Equal(MembershipChangeOutcome.InvalidRole, result.Outcome);
    }

    [Fact]
    public async Task Remove_RefusesTheLastOwner()
    {
        var result = await _members!.RemoveAsync(_trip!.Id, Owner, Owner);

        Assert.Equal(MembershipChangeOutcome.LastOwner, result);
        Assert.NotNull(await _trips!.GetForMemberAsync(_trip.Id, Owner));
    }

    [Fact]
    public async Task Remove_EditorEndsAccessImmediately()
    {
        await AddEditorAsync();

        var result = await _members!.RemoveAsync(_trip!.Id, Owner, Editor);

        Assert.Equal(MembershipChangeOutcome.Changed, result);
        Assert.Null(await _trips!.GetForMemberAsync(_trip!.Id, Editor));
    }

    [Fact]
    public async Task Remove_RefusesAnEditorAsActor()
    {
        await AddEditorAsync();

        var result = await _members!.RemoveAsync(_trip!.Id, Editor, Owner);

        Assert.Equal(MembershipChangeOutcome.Forbidden, result);
        Assert.NotNull(await _trips!.GetForMemberAsync(_trip!.Id, Owner));
    }

    [Fact]
    public async Task Update_TripAllowsAnEditorAndRefusesANonMember()
    {
        await AddEditorAsync();

        var access = await _trips!.GetForMemberAsync(_trip!.Id, Editor);

        Assert.NotNull(access);

        access.Trip.Rename("Japan 2027");

        Assert.Equal(
            TripWriteOutcome.Written,
            await _trips.UpdateAsync(access.Trip, Editor));

        Assert.Equal(
            TripWriteOutcome.NotFound,
            await _trips.UpdateAsync(_trip, Stranger));
    }

    [Fact]
    public async Task Delete_TripIsOwnerOnly()
    {
        await AddEditorAsync();

        Assert.Equal(
            TripDeleteOutcome.Forbidden,
            await _trips!.DeleteForMemberAsync(_trip!.Id, Editor));

        Assert.NotNull(await _trips.GetForMemberAsync(_trip.Id, Owner));

        Assert.Equal(
            TripDeleteOutcome.Deleted,
            await _trips.DeleteForMemberAsync(_trip.Id, Owner));

        Assert.Null(await _trips.GetForMemberAsync(_trip.Id, Owner));
    }

    [Fact]
    public async Task Accept_OfTwoCallersOnlyOneWins()
    {
        await InviteAsync("stranger@example.com", Hash(1));

        await using var first = NewContext();
        await using var second = NewContext();

        var results = await Task.WhenAll(
            new InvitationRepository(first).AcceptAsync(Hash(1), Stranger, CreatedAt.AddHours(1)),
            new InvitationRepository(second).AcceptAsync(Hash(1), Stranger, CreatedAt.AddHours(1)));

        Assert.Equal(1, results.Count(result => result.Outcome == InvitationAcceptOutcome.Accepted));
        Assert.Equal(1, results.Count(result => result.Outcome == InvitationAcceptOutcome.AlreadyUsed));

        await using var check = NewContext();

        Assert.Equal(
            1,
            await check.TripMembers.CountAsync(
                member => member.TripId == _trip!.Id && member.UserId == StrangerId));
    }

    [Fact]
    public async Task ChangeRole_OfTwoOwnersOnlyOneCanStepDown()
    {
        await AddEditorAsync();

        var promoted = await _members!.ChangeRoleAsync(
            _trip!.Id,
            Owner,
            Editor,
            TripMemberRole.Owner);

        Assert.Equal(MembershipChangeOutcome.Changed, promoted.Outcome);

        await using var first = NewContext();
        await using var second = NewContext();

        var results = await Task.WhenAll(
            new TripMembershipRepository(first).ChangeRoleAsync(
                _trip.Id, Owner, Editor, TripMemberRole.Editor),
            new TripMembershipRepository(second).ChangeRoleAsync(
                _trip.Id, Editor, Owner, TripMemberRole.Editor));

        Assert.Equal(1, results.Count(result => result.Outcome == MembershipChangeOutcome.Changed));

        // The winner may have demoted the other caller, who is then no longer
        // an owner and gets Forbidden; if not, the second sees one owner left.
        Assert.Equal(
            1,
            results.Count(result =>
                result.Outcome is MembershipChangeOutcome.Forbidden or MembershipChangeOutcome.LastOwner));

        await using var check = NewContext();

        Assert.Equal(
            1,
            await check.TripMembers.CountAsync(
                member => member.TripId == _trip.Id && member.Role == TripMemberRole.Owner));
    }

    private static TabijiDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new TabijiDbContext(options);
    }

    private async Task AddEditorAsync()
    {
        _context!.TripMembers.Add(
            TripMember.Grant(_trip!.Id, Editor, TripMemberRole.Editor, CreatedAt));

        await _context.SaveChangesAsync();
    }

    private Task<CreateInvitationResult> InviteAsync(string email, byte[] hash)
    {
        return _invitations!.CreateAsync(
            _trip!.Id,
            Owner,
            EmailAddress.Create(email),
            hash,
            CreatedAt,
            CreatedAt.AddDays(7));
    }

    private async Task<int> CountMembershipAsync(Guid userId)
    {
        await using var context = NewContext();

        return await context.TripMembers.CountAsync(
            member => member.TripId == _trip!.Id && member.UserId == userId);
    }

    private static byte[] Hash(int seed) =>
        [.. Enumerable.Range(seed, 32).Select(value => (byte)(value % 256))];

    private static async Task SeedAccountAsync(Guid userId, string email)
    {
        await using var connection = new SqlConnection(ConnectionString);
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
}
