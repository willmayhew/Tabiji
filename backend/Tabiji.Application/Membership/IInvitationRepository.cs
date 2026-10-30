using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// The invitations, as the application sees them.
///
/// A token is passed as its hash: the repository never handles the raw token
/// and never stores it. Acceptance, decline and expiry are conditional writes
/// on the row's status, so two callers cannot both win.
/// </summary>
public interface IInvitationRepository
{
    Task<CreateInvitationResult> CreateAsync(
        Guid tripId,
        UserId actor,
        EmailAddress email,
        byte[] tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<InvitationListResult> ListForTripAsync(
        Guid tripId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<InvitationActionResult> RevokeAsync(
        Guid tripId,
        UserId actor,
        Guid invitationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<InvitationPreviewResult> PreviewAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<AcceptInvitationResult> AcceptAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<AcceptInvitationResult> AcceptByIdAsync(
        Guid invitationId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<InvitationActionResult> DeclineAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<InvitationActionResult> DeclineByIdAsync(
        Guid invitationId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecipientInvitationResponse>> ListForRecipientAsync(
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
