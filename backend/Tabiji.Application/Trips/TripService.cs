using Tabiji.Application.Abstractions;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

public sealed class TripService(ITripRepository repository, TimeProvider timeProvider)
{
    public async Task<TripResponse> CreateAsync(
        CreateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = Trip.Create(
            request.Name,
            request.Destination,
            request.StartsOn,
            request.EndsOn,
            timeProvider.GetUtcNow());

        await repository.AddAsync(trip, cancellationToken);

        return ToResponse(trip);
    }

    public async Task<TripResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);

        return trip is null ? null : ToResponse(trip);
    }

    private static TripResponse ToResponse(Trip trip) => new(
        trip.Id,
        trip.Name,
        trip.Destination,
        trip.StartsOn,
        trip.EndsOn,
        trip.Status);
}
