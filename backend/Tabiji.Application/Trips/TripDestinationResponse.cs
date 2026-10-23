namespace Tabiji.Application.Trips;

/// <summary>
/// A stop on a trip's route, as the API presents it.
/// </summary>
/// <param name="Id">The identity of the stop, so that a client can name it
/// again when it updates the trip.</param>
/// <param name="Name">The place, as free text.</param>
/// <param name="SortOrder">The position of the stop on the route, counting from zero.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
/// <param name="Latitude">The latitude of a place chosen from a map, or null.</param>
/// <param name="Longitude">The longitude of a place chosen from a map, or null.</param>
public sealed record TripDestinationResponse(
    Guid Id,
    string Name,
    int SortOrder,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    double? Latitude,
    double? Longitude);
