using Tabiji.Application.Abstractions;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

public sealed class TripService(ITripRepository repository, TimeProvider timeProvider)
{
    /// <summary>
    /// Creates a trip and makes <paramref name="owner"/> its owner. The owner
    /// is the account the request was authenticated as; it is never taken from
    /// the request body.
    /// </summary>
    public async Task<TripResponse> CreateAsync(
        CreateTripRequest request,
        UserId owner,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var destinations = (request.Destinations ?? [])
            .Select(destination => new TripDestinationDraft(
                null,
                destination.Name,
                destination.StartsOn,
                destination.EndsOn,
                destination.Latitude,
                destination.Longitude,
                (destination.Stops ?? [])
                    .Select(stop => new TripStopDraft(
                        null,
                        stop.Name,
                        stop.StartsOn,
                        stop.EndsOn,
                        stop.Latitude,
                        stop.Longitude,
                        stop.Category,
                        stop.Address,
                        stop.Notes,
                        stop.PlannedOn,
                        now))
                    .ToArray(),
                destination.Category,
                destination.Address,
                destination.Notes,
                destination.PlannedOn))
            .ToArray();

        var trip = Trip.Create(
            request.Name,
            destinations,
            request.StartsOn,
            request.EndsOn,
            now);

        await repository.AddAsync(trip, TripMember.Own(trip.Id, owner, now), cancellationToken);

        return ToResponse(trip, TripMemberRole.Owner);
    }

    /// <summary>
    /// Applies a whole trip to an existing one.
    ///
    /// The route is checked in full before anything is changed, so a request
    /// the domain refuses leaves the trip exactly as it was.
    ///
    /// Returns null when there is no such trip, or when the caller is not one
    /// of its members. The two are deliberately the same answer. A member
    /// whose role may not edit is also null here; the repository is what drew
    /// the line, and the endpoint keeps the answer opaque.
    /// </summary>
    public async Task<TripResponse?> UpdateAsync(
        Guid id,
        UpdateTripRequest request,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        var access = await repository.GetForMemberAsync(id, actor, cancellationToken);

        if (access is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        var destinations = (request.Destinations ?? [])
            .Select(destination => new TripDestinationDraft(
                destination.Id,
                destination.Name,
                destination.StartsOn,
                destination.EndsOn,
                destination.Latitude,
                destination.Longitude,
                (destination.Stops ?? [])
                    .Select(stop => new TripStopDraft(
                        stop.Id,
                        stop.Name,
                        stop.StartsOn,
                        stop.EndsOn,
                        stop.Latitude,
                        stop.Longitude,
                        stop.Category,
                        stop.Address,
                        stop.Notes,
                        stop.PlannedOn,
                        stop.Id is null ? now : null))
                    .ToArray(),
                destination.Category,
                destination.Address,
                destination.Notes,
                destination.PlannedOn))
            .ToArray();

        var places = request.UnfiledPlaces?
            .Select(place => new TripStopDraft(
                place.Id,
                place.Name,
                place.StartsOn,
                place.EndsOn,
                place.Latitude,
                place.Longitude,
                place.Category,
                place.Address,
                place.Notes,
                place.PlannedOn))
            .ToArray();

        access.Trip.ApplyRoute(
            request.Name,
            request.StartsOn,
            request.EndsOn,
            destinations,
            places);

        var written = await repository.UpdateAsync(access.Trip, actor, cancellationToken);

        return written == TripWriteOutcome.Written
            ? ToResponse(access.Trip, access.Role)
            : null;
    }

    public async Task<TripResponse?> GetAsync(
        Guid id,
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        var access = await repository.GetForMemberAsync(id, viewer, cancellationToken);

        return access is null ? null : ToResponse(access.Trip, access.Role);
    }

    public async Task<IReadOnlyList<TripResponse>> ListAsync(
        UserId viewer,
        CancellationToken cancellationToken = default)
    {
        var trips = await repository.ListForMemberAsync(viewer, cancellationToken);

        return trips.Select(access => ToResponse(access.Trip, access.Role)).ToArray();
    }

    public Task<TripDeleteOutcome> DeleteAsync(
        Guid id,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        return repository.DeleteForMemberAsync(id, actor, cancellationToken);
    }

    /// <summary>
    /// The destinations, their stops and the legacy unfiled places are ordered
    /// here, so the contract holds whichever repository produced the trip. A
    /// stop is never part of the main route: it is nested inside the
    /// destination it belongs to.
    /// </summary>
    private static TripResponse ToResponse(Trip trip, TripMemberRole viewerRole) => new(
        trip.Id,
        trip.Name,
        trip.ParentDestinations
            .Select(destination => new TripDestinationResponse(
                destination.Id,
                destination.Name,
                destination.SortOrder,
                destination.StartsOn,
                destination.EndsOn,
                destination.Latitude,
                destination.Longitude,
                trip.StopsOf(destination.Id)
                    .Select(ToStopResponse)
                    .ToArray(),
                destination.Category,
                destination.Address,
                destination.Notes,
                destination.PlannedOn,
                destination.CreatedAt))
            .ToArray(),
        trip.StartsOn,
        trip.EndsOn,
        trip.Status,
        viewerRole,
        trip.UnfiledPlaces.Select(ToStopResponse).ToArray());

    private static TripStopResponse ToStopResponse(TripDestination stop) => new(
        stop.Id,
        stop.Name,
        stop.SortOrder,
        stop.StartsOn,
        stop.EndsOn,
        stop.Latitude,
        stop.Longitude,
        stop.Category,
        stop.Address,
        stop.Notes,
        stop.PlannedOn,
        stop.CreatedAt);
}
