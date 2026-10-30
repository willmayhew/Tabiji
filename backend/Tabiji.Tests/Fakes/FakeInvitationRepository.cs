using Tabiji.Application.Membership;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Tests.Fakes;

/// <summary>
/// The invitation repository, in memory, for the application service's own
/// tests. It records what the service asked it to do and returns whatever the
/// test configured.
/// </summary>
internal sealed class FakeInvitationRepository : IInvitationRepository
{
    public CreateInvitationResult CreateResult { get; set; } =
        new(InvitationCreateOutcome.Created);

    public InvitationPreviewResult PreviewResult { get; set; } =
        new(InvitationPreviewOutcome.NotFound);

    public AcceptInvitationResult AcceptResult { get; set; } =
        new(InvitationAcceptOutcome.NotFound);

    public InvitationActionResult DeclineResult { get; set; } =
        new(InvitationActionOutcome.NotFound);

    public InvitationActionResult RevokeResult { get; set; } =
        new(InvitationActionOutcome.NotFound);

    public InvitationListResult ListForTripResult { get; set; } =
        new(InvitationListOutcome.Listed, []);

    public IReadOnlyList<RecipientInvitationResponse> RecipientResult { get; set; } = [];

    public (Guid TripId, UserId Actor, EmailAddress Email, byte[] Hash, DateTimeOffset Now, DateTimeOffset ExpiresAt)? LastCreate { get; private set; }

    public (byte[] Hash, UserId Viewer, DateTimeOffset Now)? LastPreview { get; private set; }

    public (byte[] Hash, UserId Viewer, DateTimeOffset Now)? LastAccept { get; private set; }

    public (Guid InvitationId, UserId Viewer, DateTimeOffset Now)? LastAcceptById { get; private set; }

    public (byte[] Hash, UserId Viewer, DateTimeOffset Now)? LastDecline { get; private set; }

    public (Guid InvitationId, UserId Viewer, DateTimeOffset Now)? LastDeclineById { get; private set; }

    public (Guid TripId, UserId Viewer, DateTimeOffset Now)? LastListForTrip { get; private set; }

    public (Guid TripId, UserId Actor, Guid InvitationId, DateTimeOffset Now)? LastRevoke { get; private set; }

    public (UserId Viewer, DateTimeOffset Now)? LastRecipientList { get; private set; }

    public Task<CreateInvitationResult> CreateAsync(
        Guid tripId,
        UserId actor,
        EmailAddress email,
        byte[] tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        LastCreate = (tripId, actor, email, tokenHash, now, expiresAt);

        return Task.FromResult(CreateResult);
    }

    public Task<InvitationListResult> ListForTripAsync(
        Guid tripId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        LastListForTrip = (tripId, viewer, now);

        return Task.FromResult(ListForTripResult);
    }

    public Task<InvitationActionResult> RevokeAsync(
        Guid tripId,
        UserId actor,
        Guid invitationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        LastRevoke = (tripId, actor, invitationId, now);

        return Task.FromResult(RevokeResult);
    }

    public Task<InvitationPreviewResult> PreviewAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        LastPreview = (tokenHash, viewer, now);

        return Task.FromResult(PreviewResult);
    }

    public Task<AcceptInvitationResult> AcceptAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        LastAccept = (tokenHash, viewer, now);

        return Task.FromResult(AcceptResult);
    }

    public Task<AcceptInvitationResult> AcceptByIdAsync(
        Guid invitationId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        LastAcceptById = (invitationId, viewer, now);

        return Task.FromResult(AcceptResult);
    }

    public Task<InvitationActionResult> DeclineAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        LastDecline = (tokenHash, viewer, now);

        return Task.FromResult(DeclineResult);
    }

    public Task<InvitationActionResult> DeclineByIdAsync(
        Guid invitationId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        LastDeclineById = (invitationId, viewer, now);

        return Task.FromResult(DeclineResult);
    }

    public Task<IReadOnlyList<RecipientInvitationResponse>> ListForRecipientAsync(
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        LastRecipientList = (viewer, now);

        return Task.FromResult(RecipientResult);
    }
}
