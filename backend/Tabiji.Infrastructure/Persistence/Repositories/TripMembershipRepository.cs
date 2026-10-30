using System.Data;
using Microsoft.EntityFrameworkCore;
using Tabiji.Application.Membership;
using Tabiji.Domain.Membership;

namespace Tabiji.Infrastructure.Persistence.Repositories;

/// <summary>
/// Membership, read and changed.
///
/// The last-owner rule cannot be enforced by a single row, so the mutations
/// run in a serializable transaction that locks the trip's membership rows.
/// Two owners trying to step down at the same time therefore cannot both see
/// two owners and leave none.
/// </summary>
public sealed class TripMembershipRepository(TabijiDbContext dbContext) : ITripMembershipRepository
{
    public async Task<TripMembersResponse?> ListAsync(
        Guid tripId,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        var viewerRole = await ViewfinderRoleAsync(tripId, viewer, cancellationToken);

        if (viewerRole is null)
        {
            return null;
        }

        var members = await (
                from member in dbContext.TripMembers
                join account in dbContext.Users on member.UserId equals account.Id
                where member.TripId == tripId
                select new TripMemberResponse(
                    member.UserId,
                    account.Email ?? account.UserName ?? string.Empty,
                    member.Role,
                    member.GrantedAt))
            .ToListAsync(cancellationToken);

        var ordered = members
            .OrderBy(member => member.Role == TripMemberRole.Owner ? 0 : 1)
            .ThenBy(member => member.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new TripMembersResponse(viewerRole.Value, ordered);
    }

    public async Task<MembershipChangeResult> ChangeRoleAsync(
        Guid tripId,
        UserId actor,
        UserId member,
        TripMemberRole role,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(role))
        {
            return new MembershipChangeResult(MembershipChangeOutcome.InvalidRole);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var members = await LockMembersAsync(tripId, cancellationToken);

        var outcome = Evaluate(members, actor, member, out var target);

        if (outcome is not null)
        {
            return new MembershipChangeResult(outcome.Value);
        }

        if (target!.IsOwner && role != TripMemberRole.Owner && OwnerCount(members) == 1)
        {
            return new MembershipChangeResult(MembershipChangeOutcome.LastOwner);
        }

        target.ChangeRole(role);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // A caller may have demoted themselves; the refreshed view says so.
        var refreshed = await ListAsync(tripId, actor, cancellationToken);

        return new MembershipChangeResult(MembershipChangeOutcome.Changed, refreshed);
    }

    public async Task<MembershipChangeOutcome> RemoveAsync(
        Guid tripId,
        UserId actor,
        UserId member,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var members = await LockMembersAsync(tripId, cancellationToken);

        var outcome = Evaluate(members, actor, member, out var target);

        if (outcome is not null)
        {
            return outcome.Value;
        }

        if (target!.IsOwner && OwnerCount(members) == 1)
        {
            return MembershipChangeOutcome.LastOwner;
        }

        dbContext.TripMembers.Remove(target);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MembershipChangeOutcome.Changed;
    }

    /// <summary>
    /// Reads the viewer's role without loading the trip. A caller who is not a
    /// member has no role, which is the same answer as an unknown trip.
    /// </summary>
    private async Task<TripMemberRole?> ViewfinderRoleAsync(
        Guid tripId,
        UserId viewer,
        CancellationToken cancellationToken)
    {
        return await dbContext.TripMembers
            .Where(member => member.TripId == tripId && member.UserId == viewer.Value)
            .Select(member => (TripMemberRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Reads every membership of the trip under a lock that holds until the
    /// transaction ends. This is what makes the owner count trustworthy.
    /// </summary>
    private async Task<TripMember[]> LockMembersAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return await dbContext.TripMembers
            .FromSqlInterpolated(
                $"SELECT * FROM [TripMembers] WITH (UPDLOCK, HOLDLOCK) WHERE [TripId] = {tripId}")
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// The rules shared by promotion, demotion and removal: the actor must be
    /// on the trip and be an owner, and the target must exist. Returns null
    /// when the change may go ahead.
    /// </summary>
    private static MembershipChangeOutcome? Evaluate(
        IReadOnlyList<TripMember> members,
        UserId actor,
        UserId member,
        out TripMember? target)
    {
        var actorMember = members.SingleOrDefault(candidate => candidate.UserId == actor.Value);

        if (actorMember is null)
        {
            target = null;
            return MembershipChangeOutcome.NotFound;
        }

        if (!actorMember.IsOwner)
        {
            target = null;
            return MembershipChangeOutcome.Forbidden;
        }

        target = members.SingleOrDefault(candidate => candidate.UserId == member.Value);

        return target is null ? MembershipChangeOutcome.NotFound : null;
    }

    private static int OwnerCount(IReadOnlyList<TripMember> members) =>
        members.Count(candidate => candidate.IsOwner);
}
