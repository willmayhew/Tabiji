using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// The people on a trip, as the application changes them.
///
/// Every operation names both the trip and the account asking. A caller who
/// is not on the trip is <see cref="MembershipChangeOutcome.NotFound"/>; a
/// member who is not an owner is <see cref="MembershipChangeOutcome.Forbidden"/>.
/// </summary>
public interface ITripMembershipRepository
{
    /// <summary>
    /// The trip's members and the caller's role, or null when the caller is
    /// not on the trip. Any member may read this.
    /// </summary>
    Task<TripMembersResponse?> ListAsync(
        Guid tripId,
        UserId viewer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes one member's role. The last owner cannot be demoted; the check
    /// and the write happen under a lock so two owners cannot both step down.
    /// </summary>
    Task<MembershipChangeResult> ChangeRoleAsync(
        Guid tripId,
        UserId actor,
        UserId member,
        TripMemberRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one member. The last owner cannot be removed. Removal is the
    /// whole revocation: every trip query already joins the membership row, so
    /// access ends with it.
    /// </summary>
    Task<MembershipChangeOutcome> RemoveAsync(
        Guid tripId,
        UserId actor,
        UserId member,
        CancellationToken cancellationToken = default);
}
