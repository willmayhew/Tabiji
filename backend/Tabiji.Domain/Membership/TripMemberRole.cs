namespace Tabiji.Domain.Membership;

/// <summary>
/// What a person may do on a trip.
///
/// Only <see cref="Owner"/> is granted today: a trip is private to the person
/// who created it. The other roles exist so that the membership row can grow
/// into collaboration without the table being rebuilt.
/// </summary>
public enum TripMemberRole
{
    Owner = 0,
    Organiser = 1,
    Member = 2
}
