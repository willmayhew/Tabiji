using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// One person on a trip, as the members panel sees them. The address is the
/// account's own; there is no display name in the product yet.
/// </summary>
public sealed record TripMemberResponse(
    Guid UserId,
    string Email,
    TripMemberRole Role,
    DateTimeOffset GrantedAt);
