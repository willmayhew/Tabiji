namespace Tabiji.Application.Trips;

/// <summary>
/// A stop on a trip's route, as the API asks for it when a trip is created.
/// </summary>
/// <param name="Name">The place, as free text.</param>
/// <param name="StartsOn">The planned first day, or null when not decided.</param>
/// <param name="EndsOn">The planned last day, or null when not decided.</param>
public sealed record TripDestinationRequest(string Name, DateOnly? StartsOn, DateOnly? EndsOn);
