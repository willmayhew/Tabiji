namespace Tabiji.Application.Trips;

public sealed record CreateTripRequest(
    string Name,
    string Destination,
    DateOnly StartsOn,
    DateOnly EndsOn);
