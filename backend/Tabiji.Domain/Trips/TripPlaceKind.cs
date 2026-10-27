namespace Tabiji.Domain.Trips;

/// <summary>
/// What a place on a trip is.
///
/// The hierarchy is one level deep: a destination is on the main route, a stop
/// belongs to one of those destinations, and a place is a legacy saved place
/// that has not yet been filed under a destination. New places are always
/// created as stops; <see cref="Place"/> exists so that old saved-place records
/// can be kept without inventing a parent for them.
/// </summary>
public enum TripPlaceKind
{
    Destination = 0,
    Stop = 1,
    Place = 2
}
