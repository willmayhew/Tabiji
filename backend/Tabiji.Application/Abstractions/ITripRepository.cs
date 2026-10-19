using Tabiji.Domain.Trips;

namespace Tabiji.Application.Abstractions;

public interface ITripRepository
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken = default);

    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every trip, ordered by start date and then by name.
    /// </summary>
    Task<IReadOnlyList<Trip>> ListAsync(CancellationToken cancellationToken = default);
}
