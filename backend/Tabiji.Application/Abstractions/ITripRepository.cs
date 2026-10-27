using Tabiji.Domain.Trips;

namespace Tabiji.Application.Abstractions;

public interface ITripRepository
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the trip with its route loaded, ordered by sort order, or null
    /// when there is no such trip.
    /// </summary>
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every trip with its route loaded, ordered by start date and
    /// then by name.
    /// </summary>
    Task<IReadOnlyList<Trip>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the trip and its route back, in one go, so that a trip is never
    /// left half changed.
    /// </summary>
    Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the trip and its route when found.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
