using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A saved place as the API presents it.
///
/// The owning trip is not repeated: a place is always reached through its
/// trip, so the route already says which trip it belongs to.
/// </summary>
/// <param name="Id">The identity of the place, so that a client can name it
/// again when it changes or removes it.</param>
/// <param name="DestinationId">The stop the place is filed under, or null.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="Latitude">The latitude of the place. Always given.</param>
/// <param name="Longitude">The longitude of the place. Always given.</param>
/// <param name="Category">What kind of place it is, or null when not said.</param>
/// <param name="Address">A short description of where it is, or null.</param>
/// <param name="Notes">The reader's own note, or null.</param>
/// <param name="CreatedAt">When the place was saved.</param>
public sealed record SavedPlaceResponse(
    Guid Id,
    Guid? DestinationId,
    string Name,
    double Latitude,
    double Longitude,
    PlaceCategory? Category,
    string? Address,
    string? Notes,
    DateTimeOffset CreatedAt);
