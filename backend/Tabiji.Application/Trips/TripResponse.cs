using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

public sealed record TripResponse(
    Guid Id,
    string Name,
    IReadOnlyList<TripDestinationResponse> Destinations,
    DateOnly StartsOn,
    DateOnly EndsOn,
    TripStatus Status,
    TripMemberRole ViewerRole,
    IReadOnlyList<TripStopResponse>? UnfiledPlaces = null);
