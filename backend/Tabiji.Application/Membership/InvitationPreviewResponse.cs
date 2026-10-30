using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// What the invitation link shows before the recipient decides. It is only
/// returned to the address the invitation names.
/// </summary>
public sealed record InvitationPreviewResponse(
    Guid TripId,
    string TripName,
    string InvitedByEmail,
    TripMemberRole Role,
    InvitationStatus Status,
    DateTimeOffset ExpiresAt,
    bool CanAccept);
