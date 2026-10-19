using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Tests.Fakes;

internal sealed class FakeTripRepository : ITripRepository
{
    private readonly Dictionary<Guid, Trip> _trips = [];

    public FakeTripRepository()
    {
    }

    public FakeTripRepository(IEnumerable<Trip> seed)
    {
        foreach (var trip in seed)
        {
            _trips[trip.Id] = trip;
        }
    }

    public int AddCallCount { get; private set; }

    public Task AddAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        _trips[trip.Id] = trip;
        AddCallCount++;

        return Task.CompletedTask;
    }

    public Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_trips.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<Trip>> ListAsync(CancellationToken cancellationToken = default)
    {
        var ordered = _trips.Values
            .OrderBy(trip => trip.StartsOn)
            .ThenBy(trip => trip.Name)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Trip>>(ordered);
    }
}
