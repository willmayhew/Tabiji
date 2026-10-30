using System.Security.Cryptography;
using System.Text;
using Tabiji.Application.Membership;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;
using Tabiji.Tests.Fakes;

namespace Tabiji.Tests;

/// <summary>
/// The application service's own work: issuing a token, deciding the expiry,
/// hashing a presented token, and keeping the raw token out of a refusal.
/// </summary>
public sealed class InvitationServiceTests
{
    private static readonly Guid TripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private static InvitationService CreateService(
        FakeInvitationRepository repository,
        FakeInvitationTokens tokens)
    {
        return new InvitationService(
            repository,
            tokens,
            new InvitationPolicy(Lifetime),
            new FixedTimeProvider(Now));
    }

    private static TripInvitationResponse Invitation() => new(
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        TripId,
        "friend@example.com",
        TripMemberRole.Editor,
        InvitationStatus.Pending,
        Now,
        Now.Add(Lifetime),
        "owner@example.com");

    [Fact]
    public async Task Create_IssuesATokenAndPassesOnlyItsHashToTheRepository()
    {
        var repository = new FakeInvitationRepository
        {
            CreateResult = new CreateInvitationResult(InvitationCreateOutcome.Created, Invitation()),
        };
        var tokens = new FakeInvitationTokens();
        var service = CreateService(repository, tokens);

        var result = await service.CreateAsync(
            TripId,
            UserId.Create(ActorId),
            "  Friend@Example.COM  ");

        Assert.Equal(InvitationCreateOutcome.Created, result.Outcome);
        Assert.Equal(tokens.Next.Token, result.Token);
        Assert.NotNull(result.Invitation);

        var call = repository.LastCreate;

        Assert.NotNull(call);
        Assert.Equal(TripId, call.Value.TripId);
        Assert.Equal(ActorId, call.Value.Actor.Value);
        Assert.Equal("Friend@Example.COM", call.Value.Email.Value);
        Assert.Equal("FRIEND@EXAMPLE.COM", call.Value.Email.Normalized);
        Assert.Equal(tokens.Next.Hash, call.Value.Hash);
        Assert.Equal(Now, call.Value.Now);
        Assert.Equal(Now.Add(Lifetime), call.Value.ExpiresAt);
    }

    [Theory]
    [InlineData(InvitationCreateOutcome.AlreadyMember)]
    [InlineData(InvitationCreateOutcome.AlreadyInvited)]
    [InlineData(InvitationCreateOutcome.Forbidden)]
    [InlineData(InvitationCreateOutcome.NotFound)]
    public async Task Create_DoesNotHandOutATokenWhenTheInvitationWasNotWritten(
        InvitationCreateOutcome outcome)
    {
        var repository = new FakeInvitationRepository
        {
            CreateResult = new CreateInvitationResult(outcome),
        };
        var service = CreateService(repository, new FakeInvitationTokens());

        var result = await service.CreateAsync(
            TripId,
            UserId.Create(ActorId),
            "friend@example.com");

        Assert.Equal(outcome, result.Outcome);
        Assert.Null(result.Token);
        Assert.Null(result.Invitation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    public async Task Create_RefusesAnAddressThatIsNotOne(string? email)
    {
        var repository = new FakeInvitationRepository();
        var service = CreateService(repository, new FakeInvitationTokens());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(TripId, UserId.Create(ActorId), email));

        Assert.Null(repository.LastCreate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Preview_DoesNotAskTheRepositoryAboutAnEmptyToken(string? token)
    {
        var repository = new FakeInvitationRepository();
        var service = CreateService(repository, new FakeInvitationTokens());

        var result = await service.PreviewAsync(token, UserId.Create(ActorId));

        Assert.Equal(InvitationPreviewOutcome.NotFound, result.Outcome);
        Assert.Null(repository.LastPreview);
    }

    [Fact]
    public async Task Preview_HashesThePresentedToken()
    {
        var repository = new FakeInvitationRepository();
        var service = CreateService(repository, new FakeInvitationTokens());

        await service.PreviewAsync("the-token", UserId.Create(ActorId));

        var call = repository.LastPreview;

        Assert.NotNull(call);
        Assert.Equal(
            SHA256.HashData(Encoding.ASCII.GetBytes("the-token")),
            call.Value.Hash);
        Assert.Equal(ActorId, call.Value.Viewer.Value);
        Assert.Equal(Now, call.Value.Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Accept_DoesNotAskTheRepositoryAboutAnEmptyToken(string? token)
    {
        var repository = new FakeInvitationRepository();
        var service = CreateService(repository, new FakeInvitationTokens());

        var result = await service.AcceptAsync(token, UserId.Create(ActorId));

        Assert.Equal(InvitationAcceptOutcome.NotFound, result.Outcome);
        Assert.Null(repository.LastAccept);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Decline_DoesNotAskTheRepositoryAboutAnEmptyToken(string? token)
    {
        var repository = new FakeInvitationRepository();
        var service = CreateService(repository, new FakeInvitationTokens());

        var result = await service.DeclineAsync(token, UserId.Create(ActorId));

        Assert.Equal(InvitationActionOutcome.NotFound, result.Outcome);
        Assert.Null(repository.LastDecline);
    }

    [Fact]
    public async Task AcceptById_PassesTheMomentTheServiceRead()
    {
        var repository = new FakeInvitationRepository
        {
            AcceptResult = new AcceptInvitationResult(
                InvitationAcceptOutcome.Accepted,
                new AcceptedInvitation(TripId, TripMemberRole.Editor)),
        };
        var service = CreateService(repository, new FakeInvitationTokens());
        var invitationId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var result = await service.AcceptByIdAsync(invitationId, UserId.Create(ActorId));

        Assert.Equal(InvitationAcceptOutcome.Accepted, result.Outcome);
        Assert.Equal(TripId, result.Accepted?.TripId);

        var call = repository.LastAcceptById;

        Assert.NotNull(call);
        Assert.Equal(invitationId, call.Value.InvitationId);
        Assert.Equal(Now, call.Value.Now);
    }

    [Fact]
    public async Task ListForRecipient_PassesTheMomentTheServiceRead()
    {
        var repository = new FakeInvitationRepository();
        var service = CreateService(repository, new FakeInvitationTokens());

        await service.ListForRecipientAsync(UserId.Create(ActorId));

        var call = repository.LastRecipientList;

        Assert.NotNull(call);
        Assert.Equal(ActorId, call.Value.Viewer.Value);
        Assert.Equal(Now, call.Value.Now);
    }
}
