using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>The role a member should have. The repository checks it is a role.</summary>
public sealed record ChangeMemberRoleRequest(TripMemberRole Role);
