namespace Tabiji.Application.Membership;

/// <summary>
/// What happened when an invitation was declined or revoked. A terminal
/// invitation the caller is entitled to know about reports which state it is
/// in; a revocation of a row that is no longer pending is simply not found.
/// </summary>
public enum InvitationActionOutcome
{
    Changed = 0,
    NotFound = 1,
    Forbidden = 2,
    Expired = 3,
    AlreadyUsed = 4,
    Declined = 5,
    Revoked = 6
}
