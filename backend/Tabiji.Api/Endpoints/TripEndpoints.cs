using Tabiji.Application.Trips;

namespace Tabiji.Api.Endpoints;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/trips").WithTags("Trips");

        group.MapPost(
            "/",
            async (
                CreateTripRequest request,
                TripService tripService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var trip = await tripService.CreateAsync(request, cancellationToken);

                    IResult result = Results.Created($"/api/trips/{trip.Id}", trip);
                    return result;
                }
                catch (ArgumentException exception)
                {
                    IResult result = Results.Problem(
                        title: "Invalid trip",
                        detail: exception.Message,
                        statusCode: StatusCodes.Status400BadRequest);

                    return result;
                }
            })
            .WithName("CreateTrip")
            .WithSummary("Creates a new trip.");

        group.MapGet(
            "/{id:guid}",
            async (
                Guid id,
                TripService tripService,
                CancellationToken cancellationToken) =>
            {
                var trip = await tripService.GetAsync(id, cancellationToken);

                IResult result = trip is null
                    ? Results.Problem(
                        title: "Trip not found",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(trip);

                return result;
            })
            .WithName("GetTripById")
            .WithSummary("Returns a single trip by its identifier.");

        return endpoints;
    }
}
