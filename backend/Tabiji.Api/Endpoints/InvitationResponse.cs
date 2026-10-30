using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Api.Endpoints;

/// <summary>
/// An invitation as the trip's owners see it after creating one.
///
/// <see cref="InvitationUrl"/> is the development-only way to deliver the
/// link without an email provider. It is null everywhere else, and the token
/// itself is never a field of its own.
/// </summary>
public sealed record InvitationResponse(
    Guid Id,
    Guid TripId,
    string Email,
    TripMemberRole Role,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string InvitedByEmail,
    string? InvitationUrl);
