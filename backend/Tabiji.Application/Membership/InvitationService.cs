using Tabiji.Application.Abstractions;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// Issues and answers invitations.
///
/// This is where the raw token is made and where the expiry is decided. The
/// repository only ever sees the hash and the two moments, and every decision
/// about who may act is made against the membership rows inside the statement.
/// </summary>
public sealed class InvitationService(
    IInvitationRepository repository,
    IInvitationTokens tokens,
    InvitationPolicy policy,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Issues an invitation. An invalid address is an
    /// <see cref="ArgumentException"/>, like every other domain refusal.
    /// </summary>
    public async Task<CreatedInvitation> CreateAsync(
        Guid tripId,
        UserId actor,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var address = EmailAddress.Create(email);
        var now = timeProvider.GetUtcNow();
        var issued = tokens.Issue();

        var result = await repository.CreateAsync(
            tripId,
            actor,
            address,
            issued.Hash,
            now,
            now.Add(policy.Lifetime),
            cancellationToken);

        return new CreatedInvitation(
            result.Outcome,
            result.Invitation,
            result.Outcome == InvitationCreateOutcome.Created ? issued.Token : null);
    }

    public Task<InvitationListResult> ListForTripAsync(
        Guid tripId,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        return repository.ListForTripAsync(
            tripId,
            viewer,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<InvitationActionResult> RevokeAsync(
        Guid tripId,
        UserId actor,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        return repository.RevokeAsync(
            tripId,
            actor,
            invitationId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<InvitationPreviewResult> PreviewAsync(
        string? token,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new InvitationPreviewResult(InvitationPreviewOutcome.NotFound);
        }

        return await repository.PreviewAsync(
            tokens.Hash(token),
            viewer,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<AcceptInvitationResult> AcceptAsync(
        string? token,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new AcceptInvitationResult(InvitationAcceptOutcome.NotFound);
        }

        return await repository.AcceptAsync(
            tokens.Hash(token),
            viewer,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<AcceptInvitationResult> AcceptByIdAsync(
        Guid invitationId,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        return repository.AcceptByIdAsync(
            invitationId,
            viewer,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<InvitationActionResult> DeclineAsync(
        string? token,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new InvitationActionResult(InvitationActionOutcome.NotFound);
        }

        return await repository.DeclineAsync(
            tokens.Hash(token),
            viewer,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<InvitationActionResult> DeclineByIdAsync(
        Guid invitationId,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        return repository.DeclineByIdAsync(
            invitationId,
            viewer,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<IReadOnlyList<RecipientInvitationResponse>> ListForRecipientAsync(
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        return repository.ListForRecipientAsync(
            viewer,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
