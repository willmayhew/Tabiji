using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A destination on a trip's main route, as the API presents it, with the
/// stops within it nested in <paramref name="Stops"/>.
/// </summary>
/// <param name="Id">The identity of the destination, so that a client can name
/// it again when it updates the trip.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="SortOrder">The position of the destination on the main route,
/// counting from zero.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
/// <param name="Latitude">The latitude of a place chosen from a map, or null.</param>
/// <param name="Longitude">The longitude of a place chosen from a map, or null.</param>
/// <param name="Stops">The stops within this destination, in their own order.
/// They are never part of the main route.</param>
/// <param name="Category">What kind of place it is, or null when not said.</param>
/// <param name="Address">A short description of where it is, or null.</param>
/// <param name="Notes">The reader's own note, or null.</param>
/// <param name="PlannedOn">The day of the trip the destination is planned for, or null.</param>
/// <param name="CreatedAt">When the destination was saved, or null when not known.</param>
public sealed record TripDestinationResponse(
    Guid Id,
    string Name,
    int SortOrder,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<TripStopResponse>? Stops = null,
    PlaceCategory? Category = null,
    string? Address = null,
    string? Notes = null,
    DateOnly? PlannedOn = null,
    DateTimeOffset? CreatedAt = null);
