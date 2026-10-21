namespace Tabiji.Application.Trips;

/// <summary>
/// A request to create a trip.
///
/// The destinations are given in the order they are visited. The list is
/// nullable so that a request without it binds cleanly and is rejected by the
/// domain with a useful message, rather than failing inside the service.
/// </summary>
public sealed record CreateTripRequest(
    string Name,
    IReadOnlyList<TripDestinationRequest>? Destinations,
    DateOnly StartsOn,
    DateOnly EndsOn);
