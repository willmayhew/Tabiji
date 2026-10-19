using Tabiji.Domain.Trips;

namespace Tabiji.Application.Abstractions;

public interface ITripRepository
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken = default);

    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
