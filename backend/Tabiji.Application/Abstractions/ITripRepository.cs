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
    /// Returns the trip with its route loaded, ordered by sort order, when the
    /// account is one of its members; null when the trip does not exist or the
    /// account is not a member.
    /// </summary>
    Task<Trip?> GetForMemberAsync(
        Guid tripId,
        UserId member,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the trips the account is a member of, with their routes loaded,
    /// ordered by start date and then by name.
    /// </summary>
    Task<IReadOnlyList<Trip>> ListForMemberAsync(
        UserId member,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the trip and its route back, in one go, so that a trip is never
    /// left half changed. Returns false, writing nothing, when the account is
    /// not a member.
    /// </summary>
    Task<bool> UpdateAsync(
        Trip trip,
        UserId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the trip and its route when the account is a member of it.
    /// </summary>
    Task<bool> DeleteForMemberAsync(
        Guid tripId,
        UserId actor,
        CancellationToken cancellationToken = default);
}
