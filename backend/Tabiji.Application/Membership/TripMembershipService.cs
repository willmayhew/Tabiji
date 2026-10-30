using Tabiji.Domain.Membership;

namespace Tabiji.Application.Membership;

public sealed class TripMembershipService(ITripMembershipRepository repository)
{
    public Task<TripMembersResponse?> ListAsync(
        Guid tripId,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        return repository.ListAsync(tripId, viewer, cancellationToken);
    }

    public Task<MembershipChangeResult> ChangeRoleAsync(
        Guid tripId,
        UserId actor,
        UserId member,
        TripMemberRole role,
        CancellationToken cancellationToken = default)
    {
        return repository.ChangeRoleAsync(tripId, actor, member, role, cancellationToken);
    }

    public Task<MembershipChangeOutcome> RemoveAsync(
        Guid tripId,
        UserId actor,
        UserId member,
        CancellationToken cancellationToken = default)
    {
        return repository.RemoveAsync(tripId, actor, member, cancellationToken);
    }
}
