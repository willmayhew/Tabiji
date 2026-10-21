namespace Tabiji.Domain.Trips;

/// <summary>
/// A stop as it is being written: the identity of an existing stop, or none
/// for a new one, together with its name and its optional dates.
///
/// The draft is what a route is checked against before anything is changed,
/// which is what lets a rejected edit leave the trip exactly as it was.
/// </summary>
/// <param name="Id">The identity of the stop, or null for a new one.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
public sealed record TripDestinationDraft(
    Guid? Id,
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn)
{
    /// <summary>
    /// A new stop, with no dates decided yet.
    /// </summary>
    public static TripDestinationDraft Undated(string name) => new(null, name, null, null);
}
