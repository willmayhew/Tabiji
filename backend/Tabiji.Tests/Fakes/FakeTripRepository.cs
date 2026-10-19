using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Tests.Fakes;

internal sealed class FakeTripRepository : ITripRepository
{
    private readonly Dictionary<Guid, Trip> _trips = [];

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
}
