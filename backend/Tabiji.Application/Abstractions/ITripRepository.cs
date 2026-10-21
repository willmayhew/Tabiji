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
}
