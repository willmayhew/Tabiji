namespace Tabiji.Application.Membership;

/// <summary>The invitations of a trip, when the caller was allowed to see them.</summary>
public sealed record InvitationListResult(
    InvitationListOutcome Outcome,
    IReadOnlyList<TripInvitationResponse> Invitations);
