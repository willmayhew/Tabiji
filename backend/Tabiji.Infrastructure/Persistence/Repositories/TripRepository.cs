using Microsoft.EntityFrameworkCore;
using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Infrastructure.Persistence.Repositories;

public sealed class TripRepository(TabijiDbContext dbContext) : ITripRepository
{
    /// <summary>
    /// The positions are moved out of the way before the new ones are written,
    /// so that a stop can take the place of another without the unique index
    /// on (TripId, SortOrder) ever seeing two stops in the same position on
    /// the way.
    ///
    /// A trip holds at most twenty stops, so its positions live between 0 and
    /// 19 and the parking range can never reach them.
    /// </summary>
    private const int ParkingOffset = 1000;

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

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await dbContext.Trips
            .Where(trip => trip.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 1;
    }

    /// <summary>
    /// Writes the trip and its route back in one go.
    ///
    /// The positions are moved in the database rather than through the change
    /// tracker. The tracker is asked to move a stop into a place another stop
    /// is still holding, it reads the unique index as a circular dependency
    /// and refuses to save at all. Writing the positions here, in the
    /// database, keeps the tracker out of the way.
    ///
    /// The whole thing is committed in a single transaction, so a trip is
    /// never left half changed.
    ///
    /// The stops are updated in place rather than written afresh, so that a
    /// stop keeps its identity across an edit.
    /// </summary>
    public async Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Change tracking is off while the route is put together, so that the
        // stops the trip did not have are not mistaken for stops it already
        // had. It is turned back on before the changes are saved.
        var autoDetectChanges = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            var existing = dbContext.ChangeTracker
                .Entries<TripDestination>()
                .Where(entry => entry.Entity.TripId == trip.Id)
                .Select(entry => entry.Entity)
                .ToArray();

            var known = existing.Select(destination => destination.Id).ToHashSet();
            var wanted = trip.Destinations.Select(destination => destination.Id).ToHashSet();

            var kept = trip.Destinations
                .Where(destination => known.Contains(destination.Id))
                .ToArray();

            await ParkPositionsAsync(trip.Id, cancellationToken);

            // A stop may be moved to another destination. The parent is written
            // in the database before the final positions are, while every
            // position is still parked: two stops that swap destinations can
            // then never collide on the unique index on the way.
            await WriteParentsAsync(kept, cancellationToken);

            foreach (var destination in kept)
            {
                // The position is written in the database, below. The tracker
                // is told that the position has not changed, so that it never
                // tries to move a place into a position another place is
                // holding.
                dbContext.Entry(destination).Property(d => d.SortOrder).OriginalValue =
                    destination.SortOrder;
            }

            await WritePositionsAsync(kept, cancellationToken);

            foreach (var destination in trip.Destinations.Where(d => !known.Contains(d.Id)))
            {
                // A stop the trip did not have: it has to be inserted. Its
                // place on the route is free, because the stops that are
                // staying have already been moved to theirs.
                dbContext.Entry(destination).State = EntityState.Added;
            }

            var removed = existing
                .Where(destination => !wanted.Contains(destination.Id))
                .ToArray();

            foreach (var destination in removed)
            {
                dbContext.Entry(destination).State = EntityState.Deleted;
            }

            // A removed stop is the place itself: its category, address, notes
            // and planned day are columns on the same row, so deleting the row
            // removes them with it. There is no separate saved-place table to
            // unlink. Unfiled places are not children of a stop and are left
            // alone.
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }

        // Change detection is back on by now: the names and the dates of the
        // stops that are staying have to be seen before they are written.
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the destination each kept stop now belongs to, while every
    /// position is still parked.
    ///
    /// The tracker is told that the parent has not changed, so that it never
    /// tries to move a stop into a destination whose parked positions would
    /// collide with another stop on the way.
    /// </summary>
    private async Task WriteParentsAsync(
        IReadOnlyList<TripDestination> destinations,
        CancellationToken cancellationToken)
    {
        foreach (var destination in destinations)
        {
            var entry = dbContext.Entry(destination);
            var parentProperty = entry.Property(candidate => candidate.ParentDestinationId);
            var kindProperty = entry.Property(candidate => candidate.Kind);

            if (parentProperty.OriginalValue == destination.ParentDestinationId &&
                kindProperty.OriginalValue == destination.Kind)
            {
                continue;
            }

            // The parent and the kind are written together. A place being
            // filed becomes a Stop and gains a parent in the same statement,
            // so the CK_TripDestinations_Kind check is never presented with a
            // Place that has a parent (or a Stop that has none).
            await dbContext.TripDestinations
                .Where(candidate => candidate.Id == destination.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            candidate => candidate.ParentDestinationId,
                            destination.ParentDestinationId)
                        .SetProperty(candidate => candidate.Kind, destination.Kind),
                    cancellationToken);

            parentProperty.OriginalValue = destination.ParentDestinationId;
            kindProperty.OriginalValue = destination.Kind;
        }
    }

    /// <summary>
    /// Moves every place of the trip out of the way of the positions that are
    /// about to be written.
    ///
    /// Every parked position is unique across the whole trip, not merely
    /// within its own destination. A stop can move from one destination to
    /// another, and two stops that swap destinations would otherwise collide
    /// on the unique index while both are parked at the same offset.
    ///
    /// This is done in the database rather than through the change tracker,
    /// which is left holding the values the domain has already decided.
    /// </summary>
    private async Task ParkPositionsAsync(Guid tripId, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             ;WITH [Parked] AS (
                 SELECT [Id], ROW_NUMBER() OVER (ORDER BY [Id]) AS [Position]
                 FROM [TripDestinations]
                 WHERE [TripId] = {tripId}
             )
             UPDATE [destination]
             SET [SortOrder] = {ParkingOffset} + [parked].[Position]
             FROM [TripDestinations] AS [destination]
             JOIN [Parked] AS [parked] ON [parked].[Id] = [destination].[Id];
             """,
            cancellationToken);
    }

    /// <summary>
    /// Writes the position each stop has been given.
    ///
    /// The stops are written one at a time. Every one of them is parked out
    /// of the way, and every one of them is given a position of its own, so
    /// no two stops ever share a position on the way.
    /// </summary>
    private async Task WritePositionsAsync(
        IReadOnlyList<TripDestination> destinations,
        CancellationToken cancellationToken)
    {
        foreach (var destination in destinations)
        {
            var position = destination.SortOrder;

            await dbContext.TripDestinations
                .Where(candidate => candidate.Id == destination.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(candidate => candidate.SortOrder, position),
                    cancellationToken);
        }
    }
}
