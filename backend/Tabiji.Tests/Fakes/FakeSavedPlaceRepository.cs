using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Tests.Fakes;

internal sealed class FakeSavedPlaceRepository : ISavedPlaceRepository
{
    private readonly Dictionary<Guid, SavedPlace> _places = [];

    public FakeSavedPlaceRepository()
    {
    }

    public FakeSavedPlaceRepository(IEnumerable<SavedPlace> seed)
    {
        foreach (var place in seed)
        {
            _places[place.Id] = place;
        }
    }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public int RemoveCallCount { get; private set; }

    public Task AddAsync(SavedPlace place, CancellationToken cancellationToken = default)
    {
        _places[place.Id] = place;
        AddCallCount++;

        return Task.CompletedTask;
    }

    public Task<SavedPlace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_places.GetValueOrDefault(id));
    }

    /// <summary>
    /// Reproduces the repository's ordering contract — by the time the place
    /// was saved and then by name — so the service is exercised against the
    /// order it will really be given.
    /// </summary>
    public Task<IReadOnlyList<SavedPlace>> ListForTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var ordered = _places.Values
            .Where(place => place.TripId == tripId)
            .OrderBy(place => place.CreatedAt)
            .ThenBy(place => place.Name)
            .ToArray();

        return Task.FromResult<IReadOnlyList<SavedPlace>>(ordered);
    }

    public Task UpdateAsync(SavedPlace place, CancellationToken cancellationToken = default)
    {
        _places[place.Id] = place;
        UpdateCallCount++;

        return Task.CompletedTask;
    }

    public Task RemoveAsync(SavedPlace place, CancellationToken cancellationToken = default)
    {
        _places.Remove(place.Id);
        RemoveCallCount++;

        return Task.CompletedTask;
    }
}
