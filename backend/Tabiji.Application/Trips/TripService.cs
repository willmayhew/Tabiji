using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

public sealed class TripService(ITripRepository repository, TimeProvider timeProvider)
{
    public async Task<TripResponse> CreateAsync(
        CreateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = Trip.Create(
            request.Name,
            request.Destinations ?? [],
            request.StartsOn,
            request.EndsOn,
            timeProvider.GetUtcNow());

        await repository.AddAsync(trip, cancellationToken);

        return ToResponse(trip);
    }

    public async Task<TripResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);

        return trip is null ? null : ToResponse(trip);
    }

    public async Task<IReadOnlyList<TripResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var trips = await repository.ListAsync(cancellationToken);

        return trips.Select(ToResponse).ToArray();
    }

    /// <summary>
    /// The stops are ordered by their sort order here, so the contract holds
    /// whichever repository produced the trip.
    /// </summary>
    private static TripResponse ToResponse(Trip trip) => new(
        trip.Id,
        trip.Name,
        trip.Destinations
            .OrderBy(destination => destination.SortOrder)
            .Select(destination => new TripDestinationResponse(destination.Name, destination.SortOrder))
            .ToArray(),
        trip.StartsOn,
        trip.EndsOn,
        trip.Status);
}
