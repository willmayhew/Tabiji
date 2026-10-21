using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

public sealed record TripResponse(
    Guid Id,
    string Name,
    IReadOnlyList<TripDestinationResponse> Destinations,
    DateOnly StartsOn,
    DateOnly EndsOn,
    TripStatus Status);
