using Tabiji.Application.Trips;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Abstractions;

/// <summary>
/// The trips, as the application sees them.
///
/// Every operation names the account that is asking. There is deliberately no
/// method that reads, writes or deletes a trip without one: a trip the caller
/// is not a member of is answered exactly as a trip that does not exist, so a
/// forgotten check cannot become a way in.
///
/// A read returns the caller's role with the trip. A write says whether the
/// role was enough, so the endpoint can tell "not yours" from "not allowed".
/// </summary>
public interface ITripRepository
{
    /// <summary>
    /// Writes a new trip and the membership that owns it, in one save, so that
    /// a trip is never left with nobody able to reach it.
    /// </summary>
    Task AddAsync(
        Trip trip,
        TripMember owner,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the trip with its route loaded, ordered by sort order, and the
    /// account's role, when the account is one of its members; null when the
    /// trip does not exist or the account is not a member.
    /// </summary>
    Task<TripAccess?> GetForMemberAsync(
        Guid tripId,
        UserId member,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the trips the account is a member of, with their routes loaded,
    /// ordered by start date and then by name, each with the account's role.
    /// </summary>
    Task<IReadOnlyList<TripAccess>> ListForMemberAsync(
        UserId member,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the trip and its route back, in one go, so that a trip is never
    /// left half changed. An owner or an editor may write; any other role is
    /// refused without writing anything.
    /// </summary>
    Task<TripWriteOutcome> UpdateAsync(
        Trip trip,
        UserId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the trip, its route and its membership when the account is one
    /// of its owners. An editor may read and change the journey but not
    /// destroy it.
    /// </summary>
    Task<TripDeleteOutcome> DeleteForMemberAsync(
        Guid tripId,
        UserId actor,
        CancellationToken cancellationToken = default);
}
