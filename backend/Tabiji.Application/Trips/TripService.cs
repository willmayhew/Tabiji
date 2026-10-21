using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

public sealed class TripService(ITripRepository repository, TimeProvider timeProvider)
{
    public async Task<TripResponse> CreateAsync(
        CreateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        var destinations = (request.Destinations ?? [])
            .Select(destination => new TripDestinationDraft(
                null,
                destination.Name,
                destination.StartsOn,
                destination.EndsOn))
            .ToArray();

        var trip = Trip.Create(
            request.Name,
            destinations,
            request.StartsOn,
            request.EndsOn,
            timeProvider.GetUtcNow());

        await repository.AddAsync(trip, cancellationToken);

        return ToResponse(trip);
    }

    /// <summary>
    /// Applies a whole trip to an existing one.
    ///
    /// The route is checked in full before anything is changed, so a request
    /// the domain refuses leaves the trip exactly as it was.
    ///
    /// Returns null when there is no such trip.
    /// </summary>
    public async Task<TripResponse?> UpdateAsync(
        Guid id,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var destinations = (request.Destinations ?? [])
            .Select(destination => new TripDestinationDraft(
                destination.Id,
                destination.Name,
                destination.StartsOn,
                destination.EndsOn))
            .ToArray();

        trip.ApplyRoute(request.Name, request.StartsOn, request.EndsOn, destinations);

        await repository.UpdateAsync(trip, cancellationToken);

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
            .Select(destination => new TripDestinationResponse(
                destination.Id,
                destination.Name,
                destination.SortOrder,
                destination.StartsOn,
                destination.EndsOn))
            .ToArray(),
        trip.StartsOn,
        trip.EndsOn,
        trip.Status);
}
