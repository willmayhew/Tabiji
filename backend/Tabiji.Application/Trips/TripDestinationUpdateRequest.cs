namespace Tabiji.Application.Trips;

/// <summary>
/// A stop on a trip's route, as the API asks for it when a trip is updated.
///
/// The identifier decides what happens to the stop: an identifier names a stop
/// that already exists and is kept, and no identifier asks for a new one. A
/// stop the request leaves out is removed.
/// </summary>
/// <param name="Id">The identifier of the stop, or null for a new one.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
public sealed record TripDestinationUpdateRequest(
    Guid? Id,
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn);
