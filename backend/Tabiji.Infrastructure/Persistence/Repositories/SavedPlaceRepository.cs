using Microsoft.EntityFrameworkCore;
using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Infrastructure.Persistence.Repositories;

public sealed class SavedPlaceRepository(TabijiDbContext dbContext) : ISavedPlaceRepository
{
    public async Task AddAsync(SavedPlace place, CancellationToken cancellationToken = default)
    {
        await dbContext.SavedPlaces.AddAsync(place, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SavedPlace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.SavedPlaces
            .SingleOrDefaultAsync(place => place.Id == id, cancellationToken);
    }

    /// <summary>
    /// The places of one trip, oldest first and then by name, so that a trip
    /// reads in the order the reader saved it.
    /// </summary>
    public async Task<IReadOnlyList<SavedPlace>> ListForTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SavedPlaces
            .Where(place => place.TripId == tripId)
            .OrderBy(place => place.CreatedAt)
            .ThenBy(place => place.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(SavedPlace place, CancellationToken cancellationToken = default)
    {
        dbContext.SavedPlaces.Update(place);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(SavedPlace place, CancellationToken cancellationToken = default)
    {
        dbContext.SavedPlaces.Remove(place);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
