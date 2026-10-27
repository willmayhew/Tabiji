using Tabiji.Application.Trips;

namespace Tabiji.Api.Endpoints;

public static class SavedPlaceEndpoints
{
    /// <summary>
    /// A read-only compatibility view of a trip's places.
    ///
    /// Places are rows of the trip's hierarchy now, and the trip API is the
    /// one surface that creates, changes or removes them. These routes are
    /// kept for one release so an older client can still read the places that
    /// carry a position; they never write.
    /// </summary>
    public static IEndpointRouteBuilder MapSavedPlaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/trips/{tripId:guid}/places")
            .WithTags("Saved places");

        group.MapGet(
            "/",
            async (
                Guid tripId,
                SavedPlaceService placeService,
                CancellationToken cancellationToken) =>
            {
                var places = await placeService.ListAsync(tripId, cancellationToken);

                IResult result = places is null
                    ? Results.Problem(
                        title: "Trip not found",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(places);

                return result;
            })
            .WithName("ListSavedPlaces")
            .WithSummary("Lists the places on a trip that carry a position.");

        group.MapGet(
            "/{placeId:guid}",
            async (
                Guid tripId,
                Guid placeId,
                SavedPlaceService placeService,
                CancellationToken cancellationToken) =>
            {
                var place = await placeService.GetAsync(tripId, placeId, cancellationToken);

                IResult result = place is null
                    ? Results.Problem(
                        title: "Saved place not found",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(place);

                return result;
            })
            .WithName("GetSavedPlaceById")
            .WithSummary("Returns a single place on a trip that carries a position.");

        return endpoints;
    }
}
