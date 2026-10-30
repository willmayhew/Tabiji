using Tabiji.Application.Abstractions;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;

namespace Tabiji.Tests.Fakes;

/// <summary>
/// The trip repository, in memory.
///
/// It scopes exactly as the real one does, so a test cannot accidentally pass
/// against a fake that is more permissive than SQL.
/// </summary>
internal sealed class FakeTripRepository : ITripRepository
{
    private readonly Dictionary<Guid, Trip> _trips = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _members = [];

    public FakeTripRepository()
    {
    }

    public FakeTripRepository(IEnumerable<Trip> seed, UserId owner)
    {
        foreach (var trip in seed)
        {
            _trips[trip.Id] = trip;
            MembersOf(trip.Id).Add(owner.Value);
        }
    }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    /// <summary>
    /// Makes an account a member without going through the API. The milestone
    /// only ever grants ownership at creation, so this exists for a test that
    /// needs a second participant.
    /// </summary>
    public void Grant(Guid tripId, UserId member) => MembersOf(tripId).Add(member.Value);

    public Task AddAsync(
        Trip trip,
        TripMember owner,
        CancellationToken cancellationToken = default)
    {
        _trips[trip.Id] = trip;
        MembersOf(trip.Id).Add(owner.UserId);
        AddCallCount++;

        return Task.CompletedTask;
    }

    public Task<Trip?> GetForMemberAsync(
        Guid tripId,
        UserId member,
        CancellationToken cancellationToken = default)
    {
        var isVisible =
            _trips.TryGetValue(tripId, out var trip) &&
            MembersOf(tripId).Contains(member.Value);

        return Task.FromResult(isVisible ? trip : null);
    }

    public Task<IReadOnlyList<Trip>> ListForMemberAsync(
        UserId member,
        CancellationToken cancellationToken = default)
    {
        var ordered = _trips.Values
            .Where(trip => MembersOf(trip.Id).Contains(member.Value))
            .OrderBy(trip => trip.StartsOn)
            .ThenBy(trip => trip.Name)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Trip>>(ordered);
    }

    public Task<bool> UpdateAsync(
        Trip trip,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        if (!_trips.ContainsKey(trip.Id) || !MembersOf(trip.Id).Contains(actor.Value))
        {
            return Task.FromResult(false);
        }

        _trips[trip.Id] = trip;
        UpdateCallCount++;

        return Task.FromResult(true);
    }

    public Task<bool> DeleteForMemberAsync(
        Guid tripId,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        if (!_trips.ContainsKey(tripId) || !MembersOf(tripId).Contains(actor.Value))
        {
            return Task.FromResult(false);
        }

        _members.Remove(tripId);

        return Task.FromResult(_trips.Remove(tripId));
    }

    private HashSet<Guid> MembersOf(Guid tripId)
    {
        if (!_members.TryGetValue(tripId, out var members))
        {
            members = [];
            _members[tripId] = members;
        }

        return members;
    }
}
