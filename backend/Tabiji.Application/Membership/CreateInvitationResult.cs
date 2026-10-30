namespace Tabiji.Application.Membership;

/// <summary>The invitation a repository managed to write, if it wrote one.</summary>
public sealed record CreateInvitationResult(
    InvitationCreateOutcome Outcome,
    TripInvitationResponse? Invitation = null);
