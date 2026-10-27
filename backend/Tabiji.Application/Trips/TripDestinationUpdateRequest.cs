using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A destination on a trip's main route, as the API asks for it when a trip is
/// updated.
///
/// The identifier decides what happens to the destination: an identifier names
/// a destination that already exists and is kept, and no identifier asks for a
/// new one. A destination the request leaves out is removed, along with its
/// stops.
/// </summary>
/// <param name="Id">The identifier of the destination, or null for a new one.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
/// <param name="Latitude">The latitude of a place chosen from a map, or null when it was typed by hand.</param>
/// <param name="Longitude">The longitude of a place chosen from a map, or null when it was typed by hand.</param>
/// <param name="Stops">The stops within this destination, in the order they are listed. May be left out.</param>
/// <param name="Category">What kind of place it is, or null when not said.</param>
/// <param name="Address">A short description of where it is, or null.</param>
/// <param name="Notes">The reader's own note, or null.</param>
/// <param name="PlannedOn">The day of the trip it is planned for, or null.</param>
public sealed record TripDestinationUpdateRequest(
    Guid? Id,
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<TripStopUpdateRequest>? Stops = null,
    PlaceCategory? Category = null,
    string? Address = null,
    string? Notes = null,
    DateOnly? PlannedOn = null);
