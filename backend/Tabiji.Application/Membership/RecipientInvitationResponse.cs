using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// An invitation waiting for the signed-in account, as the trips page shows
/// it. It carries no token: the recipient may act on it by id, and the link
/// remains the other way in.
/// </summary>
public sealed record RecipientInvitationResponse(
    Guid Id,
    Guid TripId,
    string TripName,
    string InvitedByEmail,
    TripMemberRole Role,
    InvitationStatus Status,
    DateTimeOffset ExpiresAt);
