namespace Tabiji.Domain.Membership;

/// <summary>
/// One person's part in a trip.
///
/// A trip is private to its members, and the only member a trip has today is
/// the account that created it, as its <see cref="TripMemberRole.Owner"/>.
/// Ownership is a row rather than a column on the trip so that a second
/// participant is an insert rather than a schema change.
///
/// The account is held as a plain identifier. The identity type itself lives in
/// Infrastructure, and nothing here may depend on it.
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
    /// Makes <paramref name="owner"/> the owner of a trip. This is the only way
    /// a membership is created today.
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
}
