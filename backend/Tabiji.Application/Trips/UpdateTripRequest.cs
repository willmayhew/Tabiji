namespace Tabiji.Application.Trips;

/// <summary>
/// A request to update a trip.
///
/// The request holds the whole trip: its name, its dates and its route, in
/// the order the route is visited. A destination that carries an identifier is
/// kept and changed, one without an identifier is new, and one the request
/// leaves out is removed.
///
/// The list is nullable so that a request without it binds cleanly and is
/// rejected by the domain with a useful message, rather than failing inside
/// the service.
/// </summary>
public sealed record UpdateTripRequest(
    string Name,
    IReadOnlyList<TripDestinationUpdateRequest>? Destinations,
    DateOnly StartsOn,
    DateOnly EndsOn,
    IReadOnlyList<TripStopUpdateRequest>? UnfiledPlaces = null);
