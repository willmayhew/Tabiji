using Tabiji.Application.Abstractions;
using Tabiji.Application.Trips;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;

namespace Tabiji.Tests.Fakes;

/// <summary>
/// The trip repository, in memory.
///
/// It scopes and checks roles exactly as the real one does, so a test cannot
/// accidentally pass against a fake that is more permissive than SQL.
/// </summary>
internal sealed class FakeTripRepository : ITripRepository
{
    private readonly Dictionary<Guid, Trip> _trips = [];
    private readonly Dictionary<Guid, Dictionary<Guid, TripMemberRole>> _members = [];

    public FakeTripRepository()
    {
    }

    public FakeTripRepository(IEnumerable<Trip> seed, UserId owner)
    {
        foreach (var trip in seed)
        {
            _trips[trip.Id] = trip;
            MembersOf(trip.Id)[owner.Value] = TripMemberRole.Owner;
        }
    }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    /// <summary>
    /// Makes an account a member without going through the API, for a test
    /// that needs a second participant.
    /// </summary>
    public void Grant(Guid tripId, UserId member, TripMemberRole role = TripMemberRole.Editor) =>
        MembersOf(tripId)[member.Value] = role;

    public TripMemberRole? RoleOf(Guid tripId, UserId member) =>
        MembersOf(tripId).TryGetValue(member.Value, out var role) ? role : null;

    public Task AddAsync(
        Trip trip,
        TripMember owner,
        CancellationToken cancellationToken = default)
    {
        _trips[trip.Id] = trip;
        MembersOf(trip.Id)[owner.UserId] = owner.Role;
        AddCallCount++;

        return Task.CompletedTask;
    }

    public Task<TripAccess?> GetForMemberAsync(
        Guid tripId,
        UserId member,
        CancellationToken cancellationToken = default)
    {
        if (!_trips.TryGetValue(tripId, out var trip) ||
            !MembersOf(tripId).TryGetValue(member.Value, out var role))
        {
            return Task.FromResult<TripAccess?>(null);
        }

        return Task.FromResult<TripAccess?>(new TripAccess(trip, role));
    }

    public Task<IReadOnlyList<TripAccess>> ListForMemberAsync(
        UserId member,
        CancellationToken cancellationToken = default)
    {
        var ordered = _trips.Values
            .Where(trip => MembersOf(trip.Id).ContainsKey(member.Value))
            .OrderBy(trip => trip.StartsOn)
            .ThenBy(trip => trip.Name)
            .Select(trip => new TripAccess(trip, MembersOf(trip.Id)[member.Value]))
            .ToArray();

        return Task.FromResult<IReadOnlyList<TripAccess>>(ordered);
    }

    public Task<TripWriteOutcome> UpdateAsync(
        Trip trip,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        if (!_trips.ContainsKey(trip.Id) ||
            !MembersOf(trip.Id).TryGetValue(actor.Value, out var role))
        {
            return Task.FromResult(TripWriteOutcome.NotFound);
        }

        if (role is not (TripMemberRole.Owner or TripMemberRole.Editor))
        {
            return Task.FromResult(TripWriteOutcome.Forbidden);
        }

        _trips[trip.Id] = trip;
        UpdateCallCount++;

        return Task.FromResult(TripWriteOutcome.Written);
    }

    public Task<TripDeleteOutcome> DeleteForMemberAsync(
        Guid tripId,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        if (!_trips.ContainsKey(tripId) ||
            !MembersOf(tripId).TryGetValue(actor.Value, out var role))
        {
            return Task.FromResult(TripDeleteOutcome.NotFound);
        }

        if (role != TripMemberRole.Owner)
        {
            return Task.FromResult(TripDeleteOutcome.Forbidden);
        }

        _members.Remove(tripId);

        return Task.FromResult(
            _trips.Remove(tripId) ? TripDeleteOutcome.Deleted : TripDeleteOutcome.NotFound);
    }

    private Dictionary<Guid, TripMemberRole> MembersOf(Guid tripId)
    {
        if (!_members.TryGetValue(tripId, out var members))
        {
            members = [];
            _members[tripId] = members;
        }

        return members;
    }
}
