namespace Tabiji.Domain.Trips;

/// <summary>
/// A stop within a parent destination, as it is being written: the identity of
/// an existing stop, or none for a new one, together with its name, its
/// optional dates and its optional coordinates.
///
/// A stop is one level below a destination. It never carries stops of its own:
/// the hierarchy is limited to parent destination → stop.
/// </summary>
/// <param name="Id">The identity of the stop, or null for a new one.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
/// <param name="Latitude">The latitude of the place, or null when it was typed by hand.</param>
/// <param name="Longitude">The longitude of the place, or null when it was typed by hand.</param>
/// <param name="Category">What kind of place it is, or null when not said.</param>
/// <param name="Address">A short description of where it is, or null.</param>
/// <param name="Notes">The reader's own note, or null.</param>
/// <param name="PlannedOn">The day of the trip it is planned for, or null.</param>
/// <param name="CreatedAt">When the place was saved, or null when not known.</param>
public sealed record TripStopDraft(
    Guid? Id,
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    double? Latitude = null,
    double? Longitude = null,
    PlaceCategory? Category = null,
    string? Address = null,
    string? Notes = null,
    DateOnly? PlannedOn = null,
    DateTimeOffset? CreatedAt = null)
{
    /// <summary>
    /// A new stop, with no dates, coordinates or details decided yet.
    /// </summary>
    public static TripStopDraft Undated(string name) =>
        new(null, name, null, null, null, null);
}
