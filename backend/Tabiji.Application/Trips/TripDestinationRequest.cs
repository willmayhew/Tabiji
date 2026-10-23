namespace Tabiji.Application.Trips;

/// <summary>
/// A stop on a trip's route, as the API asks for it when a trip is created.
/// </summary>
/// <param name="Name">The place, as free text.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
/// <param name="Latitude">The latitude of a place chosen from a map, or null when it was typed by hand.</param>
/// <param name="Longitude">The longitude of a place chosen from a map, or null when it was typed by hand.</param>
public sealed record TripDestinationRequest(
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    double? Latitude = null,
    double? Longitude = null);
