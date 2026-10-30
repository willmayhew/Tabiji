namespace Tabiji.Application.Membership;

/// <summary>The trip an acceptance granted, when it was accepted.</summary>
public sealed record AcceptInvitationResult(
    InvitationAcceptOutcome Outcome,
    AcceptedInvitation? Accepted = null);
