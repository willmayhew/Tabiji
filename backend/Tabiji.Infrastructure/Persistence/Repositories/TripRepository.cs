using Microsoft.EntityFrameworkCore;
using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Infrastructure.Persistence.Repositories;

public sealed class TripRepository(TabijiDbContext dbContext) : ITripRepository
{
    public async Task AddAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        await dbContext.Trips.AddAsync(trip, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Trips
            .Include(trip => trip.Destinations.OrderBy(destination => destination.SortOrder))
            .SingleOrDefaultAsync(trip => trip.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Trips
            .Include(trip => trip.Destinations.OrderBy(destination => destination.SortOrder))
            .OrderBy(trip => trip.StartsOn)
            .ThenBy(trip => trip.Name)
            .ToListAsync(cancellationToken);
    }
}
