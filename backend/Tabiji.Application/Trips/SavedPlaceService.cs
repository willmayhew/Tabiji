using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// The use cases for a trip's saved places.
///
/// A place belongs to a trip, and every request is scoped to the trip it
/// names. A place that belongs to another trip is answered as though it were
/// not there at all, so one trip can never read, change or remove another
/// trip's places.
///
/// A place may be filed under one of its trip's stops. Whether a stop really
/// belongs to the trip is a question about two aggregates, so it is answered
/// here rather than in either of them: a foreign key can say that a stop
/// exists, but not that it belongs here.
/// </summary>
public sealed class SavedPlaceService(
    ISavedPlaceRepository repository,
    ITripRepository trips,
    TimeProvider timeProvider)
{
    /// <summary>
    /// The places saved to a trip, in the order the repository keeps them.
    ///
    /// Returns null when there is no such trip.
    /// </summary>
    public async Task<IReadOnlyList<SavedPlaceResponse>?> ListAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await trips.GetByIdAsync(tripId, cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var places = await repository.ListForTripAsync(tripId, cancellationToken);

        return places.Select(ToResponse).ToArray();
    }

    /// <summary>
    /// One place of a trip, or null when the trip has no such place — which
    /// is also the answer for a place that belongs to a different trip.
    /// </summary>
    public async Task<SavedPlaceResponse?> GetAsync(
        Guid tripId,
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        var place = await repository.GetByIdAsync(placeId, cancellationToken);

        if (place is null || place.TripId != tripId)
        {
            return null;
        }

        return ToResponse(place);
    }

    /// <summary>
    /// Saves a place to a trip.
    ///
    /// Returns null when there is no such trip. A destination that is not one
    /// of the trip's stops is refused.
    /// </summary>
    public async Task<SavedPlaceResponse?> CreateAsync(
        Guid tripId,
        CreateSavedPlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = await trips.GetByIdAsync(tripId, cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var destinationId = EnsureDestinationBelongsToTrip(trip, request.DestinationId);
        var (latitude, longitude) = RequiredCoordinates(request.Latitude, request.Longitude);

        var place = SavedPlace.Create(
            tripId,
            request.Name,
            latitude,
            longitude,
            request.Category,
            request.Address,
            request.Notes,
            timeProvider.GetUtcNow());

        place.MoveToDestination(destinationId);

        await repository.AddAsync(place, cancellationToken);

        return ToResponse(place);
    }

    /// <summary>
    /// Changes a place of a trip.
    ///
    /// Returns null when there is no such trip, or the trip has no such
    /// place. A destination that is not one of the trip's stops is refused,
    /// and the association is checked again rather than taken on trust: the
    /// stop it names may have gone since the place was last written.
    /// </summary>
    public async Task<SavedPlaceResponse?> UpdateAsync(
        Guid tripId,
        Guid placeId,
        UpdateSavedPlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = await trips.GetByIdAsync(tripId, cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var place = await repository.GetByIdAsync(placeId, cancellationToken);

        if (place is null || place.TripId != tripId)
        {
            return null;
        }

        var destinationId = EnsureDestinationBelongsToTrip(trip, request.DestinationId);
        var (latitude, longitude) = RequiredCoordinates(request.Latitude, request.Longitude);

        place.Edit(
            request.Name,
            latitude,
            longitude,
            request.Category,
            request.Address,
            request.Notes);

        place.MoveToDestination(destinationId);

        await repository.UpdateAsync(place, cancellationToken);

        return ToResponse(place);
    }

    /// <summary>
    /// Removes a place from a trip. Returns false when the trip has no such
    /// place, including when the place belongs to a different trip.
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid tripId,
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        var place = await repository.GetByIdAsync(placeId, cancellationToken);

        if (place is null || place.TripId != tripId)
        {
            return false;
        }

        await repository.RemoveAsync(place, cancellationToken);

        return true;
    }

    /// <summary>
    /// A place may only be filed under a stop of its own trip. A stop of
    /// another trip is refused rather than stored.
    /// </summary>
    private static Guid? EnsureDestinationBelongsToTrip(Trip trip, Guid? destinationId)
    {
        if (destinationId is not Guid id)
        {
            return null;
        }

        if (trip.Destinations.All(destination => destination.Id != id))
        {
            throw new ArgumentException(
                "The destination does not belong to this trip.",
                nameof(destinationId));
        }

        return id;
    }

    /// <summary>
    /// A saved place always has a position. A request that leaves one out is
    /// refused here rather than read as zero, which is a real place.
    /// </summary>
    private static (double Latitude, double Longitude) RequiredCoordinates(
        double? latitude,
        double? longitude)
    {
        if (latitude is null)
        {
            throw new ArgumentException("The saved place latitude is required.", nameof(latitude));
        }

        if (longitude is null)
        {
            throw new ArgumentException("The saved place longitude is required.", nameof(longitude));
        }

        return (latitude.Value, longitude.Value);
    }

    private static SavedPlaceResponse ToResponse(SavedPlace place) => new(
        place.Id,
        place.DestinationId,
        place.Name,
        place.Latitude,
        place.Longitude,
        place.Category,
        place.Address,
        place.Notes,
        place.CreatedAt);
}
