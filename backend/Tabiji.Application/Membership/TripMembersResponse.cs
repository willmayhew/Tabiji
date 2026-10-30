using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

/// <summary>
/// The people on a trip, and the caller's own role so the screen knows which
/// controls to offer without guessing.
/// </summary>
public sealed record TripMembersResponse(
    TripMemberRole ViewerRole,
    IReadOnlyList<TripMemberResponse> Members);
