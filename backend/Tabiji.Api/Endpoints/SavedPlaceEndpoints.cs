using Tabiji.Application.Trips;

namespace Tabiji.Api.Endpoints;

public static class SavedPlaceEndpoints
{
    /// <summary>
    /// The places saved to a trip.
    ///
    /// Every route names the trip as well as the place, so a request is
    /// scoped to one trip and cannot reach another trip's places. A place
    /// that belongs elsewhere is answered as though it did not exist.
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
            .WithSummary("Lists the places saved to a trip, in the order they were saved.");

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
            .WithSummary("Returns a single place saved to a trip.");

        group.MapPost(
            "/",
            async (
                Guid tripId,
                CreateSavedPlaceRequest request,
                SavedPlaceService placeService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var place = await placeService.CreateAsync(tripId, request, cancellationToken);

                    IResult result = place is null
                        ? Results.Problem(
                            title: "Trip not found",
                            statusCode: StatusCodes.Status404NotFound)
                        : Results.Created($"/api/trips/{tripId}/places/{place.Id}", place);

                    return result;
                }
                catch (ArgumentException exception)
                {
                    IResult result = Results.Problem(
                        title: "Invalid saved place",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);

                    return result;
                }
            })
            .WithName("CreateSavedPlace")
            .WithSummary("Saves a place to a trip.");

        group.MapPut(
            "/{placeId:guid}",
            async (
                Guid tripId,
                Guid placeId,
                UpdateSavedPlaceRequest request,
                SavedPlaceService placeService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var place = await placeService.UpdateAsync(
                        tripId,
                        placeId,
                        request,
                        cancellationToken);

                    IResult result = place is null
                        ? Results.Problem(
                            title: "Saved place not found",
                            statusCode: StatusCodes.Status404NotFound)
                        : Results.Ok(place);

                    return result;
                }
                catch (ArgumentException exception)
                {
                    IResult result = Results.Problem(
                        title: "Invalid saved place",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);

                    return result;
                }
            })
            .WithName("UpdateSavedPlace")
            .WithSummary("Changes a place saved to a trip.");

        group.MapDelete(
            "/{placeId:guid}",
            async (
                Guid tripId,
                Guid placeId,
                SavedPlaceService placeService,
                CancellationToken cancellationToken) =>
            {
                var removed = await placeService.DeleteAsync(tripId, placeId, cancellationToken);

                IResult result = removed
                    ? Results.NoContent()
                    : Results.Problem(
                        title: "Saved place not found",
                        statusCode: StatusCodes.Status404NotFound);

                return result;
            })
            .WithName("DeleteSavedPlace")
            .WithSummary("Removes a place from a trip.");

        return endpoints;
    }
}
