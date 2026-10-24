using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A request to change a saved place.
///
/// The request holds the whole place: its name, its position, its category,
/// its description and its notes. A field left out is cleared rather than
/// left as it was, which is the same bargain the trip update makes.
///
/// A <paramref name="DestinationId"/> of null takes the place off the route.
/// </summary>
public sealed record UpdateSavedPlaceRequest(
    string Name,
    double? Latitude,
    double? Longitude,
    PlaceCategory? Category = null,
    string? Address = null,
    string? Notes = null,
    Guid? DestinationId = null);
