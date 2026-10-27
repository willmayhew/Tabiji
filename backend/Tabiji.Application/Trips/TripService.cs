using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

public sealed class TripService(ITripRepository repository, TimeProvider timeProvider)
{
    public async Task<TripResponse> CreateAsync(
        CreateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var destinations = (request.Destinations ?? [])
            .Select(destination => new TripDestinationDraft(
                null,
                destination.Name,
                destination.StartsOn,
                destination.EndsOn,
                destination.Latitude,
                destination.Longitude,
                (destination.Stops ?? [])
                    .Select(stop => new TripStopDraft(
                        null,
                        stop.Name,
                        stop.StartsOn,
                        stop.EndsOn,
                        stop.Latitude,
                        stop.Longitude,
                        stop.Category,
                        stop.Address,
                        stop.Notes,
                        stop.PlannedOn,
                        now))
                    .ToArray(),
                destination.Category,
                destination.Address,
                destination.Notes,
                destination.PlannedOn))
            .ToArray();

        var trip = Trip.Create(
            request.Name,
            destinations,
            request.StartsOn,
            request.EndsOn,
            now);

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

        var now = timeProvider.GetUtcNow();

        var destinations = (request.Destinations ?? [])
            .Select(destination => new TripDestinationDraft(
                destination.Id,
                destination.Name,
                destination.StartsOn,
                destination.EndsOn,
                destination.Latitude,
                destination.Longitude,
                (destination.Stops ?? [])
                    .Select(stop => new TripStopDraft(
                        stop.Id,
                        stop.Name,
                        stop.StartsOn,
                        stop.EndsOn,
                        stop.Latitude,
                        stop.Longitude,
                        stop.Category,
                        stop.Address,
                        stop.Notes,
                        stop.PlannedOn,
                        stop.Id is null ? now : null))
                    .ToArray(),
                destination.Category,
                destination.Address,
                destination.Notes,
                destination.PlannedOn))
            .ToArray();

        var places = request.UnfiledPlaces?
            .Select(place => new TripStopDraft(
                place.Id,
                place.Name,
                place.StartsOn,
                place.EndsOn,
                place.Latitude,
                place.Longitude,
                place.Category,
                place.Address,
                place.Notes,
                place.PlannedOn))
            .ToArray();

        trip.ApplyRoute(
            request.Name,
            request.StartsOn,
            request.EndsOn,
            destinations,
            places);

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

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return repository.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// The destinations, their stops and the legacy unfiled places are ordered
    /// here, so the contract holds whichever repository produced the trip. A
    /// stop is never part of the main route: it is nested inside the
    /// destination it belongs to.
    /// </summary>
    private static TripResponse ToResponse(Trip trip) => new(
        trip.Id,
        trip.Name,
        trip.ParentDestinations
            .Select(destination => new TripDestinationResponse(
                destination.Id,
                destination.Name,
                destination.SortOrder,
                destination.StartsOn,
                destination.EndsOn,
                destination.Latitude,
                destination.Longitude,
                trip.StopsOf(destination.Id)
                    .Select(ToStopResponse)
                    .ToArray(),
                destination.Category,
                destination.Address,
                destination.Notes,
                destination.PlannedOn,
                destination.CreatedAt))
            .ToArray(),
        trip.StartsOn,
        trip.EndsOn,
        trip.Status,
        trip.UnfiledPlaces.Select(ToStopResponse).ToArray());

    private static TripStopResponse ToStopResponse(TripDestination stop) => new(
        stop.Id,
        stop.Name,
        stop.SortOrder,
        stop.StartsOn,
        stop.EndsOn,
        stop.Latitude,
        stop.Longitude,
        stop.Category,
        stop.Address,
        stop.Notes,
        stop.PlannedOn,
        stop.CreatedAt);
}
