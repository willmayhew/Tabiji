using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

public sealed record TripResponse(
    Guid Id,
    string Name,
    string Destination,
    DateOnly StartsOn,
    DateOnly EndsOn,
    TripStatus Status);
