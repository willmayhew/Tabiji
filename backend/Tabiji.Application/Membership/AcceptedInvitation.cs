using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// The trip an accepted invitation gave the caller access to, and the part
/// they now have in it.
/// </summary>
public sealed record AcceptedInvitation(Guid TripId, TripMemberRole Role);
