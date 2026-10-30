using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// An invitation as the trip's owners see it. The token and its hash are never
/// part of this shape.
/// </summary>
public sealed record TripInvitationResponse(
    Guid Id,
    Guid TripId,
    string Email,
    TripMemberRole Role,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string InvitedByEmail);
