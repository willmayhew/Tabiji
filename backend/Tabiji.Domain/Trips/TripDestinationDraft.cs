namespace Tabiji.Domain.Trips;

/// <summary>
/// A destination on the main route, as it is being written: the identity of an
/// existing destination, or none for a new one, together with its name, its
/// optional dates, its optional coordinates and the stops within it.
///
/// The draft is what a route is checked against before anything is changed,
/// which is what lets a rejected edit leave the trip exactly as it was.
///
/// The hierarchy is deliberately one level deep: a destination may hold stops,
/// and a stop may not hold anything.
/// </summary>
/// <param name="Id">The identity of the destination, or null for a new one.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
/// <param name="Latitude">The latitude of the place, or null when it was typed by hand.</param>
/// <param name="Longitude">The longitude of the place, or null when it was typed by hand.</param>
/// <param name="Stops">The stops within this destination, in the order they are given.</param>
/// <param name="Category">What kind of place it is, or null when not said.</param>
/// <param name="Address">A short description of where it is, or null.</param>
/// <param name="Notes">The reader's own note, or null.</param>
/// <param name="PlannedOn">The day of the trip it is planned for, or null.</param>
/// <param name="CreatedAt">When the place was saved, or null when not known.</param>
public sealed record TripDestinationDraft(
    Guid? Id,
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<TripStopDraft>? Stops = null,
    PlaceCategory? Category = null,
    string? Address = null,
    string? Notes = null,
    DateOnly? PlannedOn = null,
    DateTimeOffset? CreatedAt = null)
{
    /// <summary>
    /// A new destination, with no dates, no coordinates and no stops decided
    /// yet.
    /// </summary>
    public static TripDestinationDraft Undated(string name) =>
        new(null, name, null, null, null, null);
}
