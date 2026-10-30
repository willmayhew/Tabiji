namespace Tabiji.Domain.Membership;

/// <summary>
/// One person's part in a trip.
///
/// A trip is private to its members. The account that creates it is its
/// <see cref="TripMemberRole.Owner"/>; anyone who accepts an invitation is an
/// <see cref="TripMemberRole.Editor"/>. Ownership is a row rather than a
/// column on the trip, so a second participant is an insert rather than a
/// schema change.
///
/// The account is held as a plain identifier. The identity type itself lives in
/// Infrastructure, and nothing here may depend on it.
///
/// The aggregate cannot enforce "a trip always has an owner": it cannot see its
/// sibling rows. That rule belongs to the repository transaction that changes
/// membership.
/// </summary>
public sealed class TripMember
{
    private TripMember()
    {
    }

    private TripMember(
        Guid tripId,
        Guid userId,
        TripMemberRole role,
        DateTimeOffset grantedAt)
    {
        TripId = tripId;
        UserId = userId;
        Role = role;
        GrantedAt = grantedAt;
    }

    public Guid TripId { get; private set; }

    public Guid UserId { get; private set; }

    public TripMemberRole Role { get; private set; }

    /// <summary>
    /// When this person was given their part in the trip.
    /// </summary>
    public DateTimeOffset GrantedAt { get; private set; }

    /// <summary>
    /// Whether this person may manage the trip and its membership.
    /// </summary>
    public bool IsOwner => Role == TripMemberRole.Owner;

    /// <summary>
    /// Makes <paramref name="owner"/> the owner of a trip. This is how a trip
    /// is created.
    /// </summary>
    public static TripMember Own(Guid tripId, UserId owner, DateTimeOffset grantedAt)
    {
        if (tripId == Guid.Empty)
        {
            throw new ArgumentException("A membership must name a trip.", nameof(tripId));
        }

        if (owner.Value == Guid.Empty)
        {
            throw new ArgumentException("A membership must name an account.", nameof(owner));
        }

        return new TripMember(tripId, owner.Value, TripMemberRole.Owner, grantedAt);
    }

    /// <summary>
    /// Adds <paramref name="member"/> to a trip in the given role. This is how
    /// an accepted invitation becomes a membership, and how an existing
    /// membership is promoted or demoted.
    /// </summary>
    public static TripMember Grant(
        Guid tripId,
        UserId member,
        TripMemberRole role,
        DateTimeOffset grantedAt)
    {
        if (tripId == Guid.Empty)
        {
            throw new ArgumentException("A membership must name a trip.", nameof(tripId));
        }

        if (member.Value == Guid.Empty)
        {
            throw new ArgumentException("A membership must name an account.", nameof(member));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException("A membership must name a role.", nameof(role));
        }

        return new TripMember(tripId, member.Value, role, grantedAt);
    }

    /// <summary>
    /// Changes a person's part in the trip. The last-owner rule is enforced by
    /// the repository, which is the only place that can see the other owners.
    /// </summary>
    public void ChangeRole(TripMemberRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException("A membership must name a role.", nameof(role));
        }

        Role = role;
    }
}
