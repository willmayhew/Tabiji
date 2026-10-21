namespace Tabiji.Application.Trips;

/// <summary>
/// A stop on a trip's route, as the API presents it.
/// </summary>
/// <param name="Name">The place, as free text.</param>
/// <param name="SortOrder">The position of the stop on the route, counting from zero.</param>
public sealed record TripDestinationResponse(string Name, int SortOrder);
