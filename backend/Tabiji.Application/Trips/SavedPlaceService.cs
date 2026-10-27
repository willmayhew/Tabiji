using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A read-only compatibility view of the places on a trip.
///
/// Places are now rows of the trip's own hierarchy: a stop within a destination
/// or a legacy unfiled place. The unified trip API is the one surface that
/// creates, changes or removes a place. This service exists for one release so
/// that the old saved-place read routes keep working while clients move to the
/// trip response. It never writes.
///
/// A row without coordinates is not offered here: the old saved-place contract
/// required a position, and a stop typed by hand has none. The trip API is the
/// complete view.
/// </summary>
public sealed class SavedPlaceService(ITripRepository trips)
{
    /// <summary>
    /// The places of a trip that carry a position, in the order they were
    /// saved and then by name. Returns null when there is no such trip.
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

        return PlacesOf(trip).Select(place => ToResponse(trip, place)).ToArray();
    }

    /// <summary>
    /// One place of a trip, or null when the trip has no such place with a
    /// position. A place belonging to a different trip is answered as null.
    /// </summary>
    public async Task<SavedPlaceResponse?> GetAsync(
        Guid tripId,
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        var trip = await trips.GetByIdAsync(tripId, cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var place = PlacesOf(trip).FirstOrDefault(candidate => candidate.Id == placeId);

        return place is null ? null : ToResponse(trip, place);
    }

    private static IEnumerable<TripDestination> PlacesOf(Trip trip) =>
        trip.Destinations
            .Where(place =>
                place.Kind != TripPlaceKind.Destination &&
                place.Latitude is not null &&
                place.Longitude is not null)
            .OrderBy(place => place.CreatedAt ?? trip.CreatedAt)
            .ThenBy(place => place.Name);

    private static SavedPlaceResponse ToResponse(Trip trip, TripDestination place) => new(
        place.Id,
        place.Kind == TripPlaceKind.Stop ? place.ParentDestinationId : null,
        place.Name,
        place.Latitude!.Value,
        place.Longitude!.Value,
        place.Category,
        place.Address,
        place.Notes,
        place.PlannedOn,
        place.CreatedAt ?? trip.CreatedAt);
}
