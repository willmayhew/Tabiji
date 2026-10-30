namespace Tabiji.Application.Membership;

/// <summary>
/// The outcome of a role change, with the members as they now stand when the
/// change went through.
/// </summary>
public sealed record MembershipChangeResult(
    MembershipChangeOutcome Outcome,
    TripMembersResponse? Members = null);
