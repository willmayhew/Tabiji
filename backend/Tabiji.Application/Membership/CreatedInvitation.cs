namespace Tabiji.Application.Membership;

/// <summary>
/// The result of issuing an invitation. <see cref="Token"/> is the raw token,
/// and exists only so the development endpoint can build a link; it is never
/// stored and never returned in production.
/// </summary>
public sealed record CreatedInvitation(
    InvitationCreateOutcome Outcome,
    TripInvitationResponse? Invitation,
    string? Token);
