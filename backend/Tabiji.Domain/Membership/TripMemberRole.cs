namespace Tabiji.Domain.Membership;

/// <summary>
/// What a person may do on a trip.
///
/// An owner controls the trip itself: its membership, its invitations and
/// whether it exists at all. An editor changes the journey — the route, the
/// stops and the places — but cannot manage who else is on it.
/// </summary>
public enum TripMemberRole
{
    Owner = 0,
    Editor = 1
}
