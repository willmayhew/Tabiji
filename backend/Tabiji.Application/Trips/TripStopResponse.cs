using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A stop within a destination, as the API presents it.
/// </summary>
/// <param name="Id">The identity of the stop, so that a client can name it
/// again when it updates the trip.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="SortOrder">The position of the stop within its destination,
/// counting from zero. It is not a place on the main route.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
/// <param name="Latitude">The latitude of a place chosen from a map, or null.</param>
/// <param name="Longitude">The longitude of a place chosen from a map, or null.</param>
/// <param name="Category">What kind of place it is, or null when not said.</param>
/// <param name="Address">A short description of where it is, or null.</param>
/// <param name="Notes">The reader's own note, or null.</param>
/// <param name="PlannedOn">The day of the trip the place is planned for, or null.</param>
/// <param name="CreatedAt">When the place was saved, or null when not known.</param>
public sealed record TripStopResponse(
    Guid Id,
    string Name,
    int SortOrder,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    double? Latitude,
    double? Longitude,
    PlaceCategory? Category = null,
    string? Address = null,
    string? Notes = null,
    DateOnly? PlannedOn = null,
    DateTimeOffset? CreatedAt = null);
