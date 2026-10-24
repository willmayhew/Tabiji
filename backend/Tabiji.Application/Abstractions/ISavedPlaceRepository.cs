using Tabiji.Domain.Trips;

namespace Tabiji.Application.Abstractions;

public interface ISavedPlaceRepository
{
    Task AddAsync(SavedPlace place, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the saved place, or null when there is no such place.
    /// </summary>
    Task<SavedPlace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every place saved to a trip, ordered by the time it was saved
    /// and then by name. The ordering is part of the contract, so a caller
    /// never has to sort a trip's places itself.
    /// </summary>
    Task<IReadOnlyList<SavedPlace>> ListForTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(SavedPlace place, CancellationToken cancellationToken = default);

    Task RemoveAsync(SavedPlace place, CancellationToken cancellationToken = default);
}
