using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A stop within a destination, as the API asks for it when a trip is created.
///
/// The hierarchy is one level deep: a stop has no stops of its own.
/// </summary>
/// <param name="Name">The place, as free text.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
/// <param name="Latitude">The latitude of a place chosen from a map, or null when it was typed by hand.</param>
/// <param name="Longitude">The longitude of a place chosen from a map, or null when it was typed by hand.</param>
/// <param name="Category">What kind of place it is, or null when not said.</param>
/// <param name="Address">A short description of where it is, or null.</param>
/// <param name="Notes">The reader's own note, or null.</param>
/// <param name="PlannedOn">The day of the trip it is planned for, or null.</param>
public sealed record TripStopRequest(
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    double? Latitude = null,
    double? Longitude = null,
    PlaceCategory? Category = null,
    string? Address = null,
    string? Notes = null,
    DateOnly? PlannedOn = null);
