namespace Tabiji.Application.Membership;

/// <summary>
/// What happened when a membership was changed.
///
/// <see cref="NotFound"/> covers both an unknown trip and a caller who is not
/// on it; <see cref="Forbidden"/> is for a member whose role is not enough;
/// <see cref="LastOwner"/> is the invariant that a trip always has an owner.
/// </summary>
public enum MembershipChangeOutcome
{
    Changed = 0,
    NotFound = 1,
    Forbidden = 2,
    LastOwner = 3,
    InvalidRole = 4,
    MemberNotFound = 5
}
