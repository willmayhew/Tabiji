using System.Security.Claims;
using Tabiji.Api.Identity;
using Tabiji.Application.Trips;

namespace Tabiji.Api.Endpoints;

/// <summary>
/// The trips a signed-in account may reach.
///
/// The group requires an account, and every operation names that account when
/// it asks the application layer for a trip. A trip the caller is not a member
/// of is answered exactly as a trip that does not exist, so an id cannot be
/// used to discover somebody else's data.
/// </summary>
public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/trips")
            .WithTags("Trips")
            .RequireAuthorization();

        group.MapPost(
            "/",
            async (
                CreateTripRequest request,
                ClaimsPrincipal user,
                TripService tripService,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var owner))
                {
                    return Results.Unauthorized();
                }

                try
                {
                    var trip = await tripService.CreateAsync(request, owner, cancellationToken);

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
            .WithSummary("Creates a new trip, owned by the signed-in account.")
            .RequireValidAntiforgeryToken();

        group.MapPut(
            "/{id:guid}",
            async (
                Guid id,
                UpdateTripRequest request,
                ClaimsPrincipal user,
                TripService tripService,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var actor))
                {
                    return Results.Unauthorized();
                }

                try
                {
                    var trip = await tripService.UpdateAsync(id, request, actor, cancellationToken);

                    IResult result = trip is null
                        ? Results.Problem(
                            title: "Trip not found",
                            statusCode: StatusCodes.Status404NotFound)
                        : Results.Ok(trip);

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
            .WithName("UpdateTrip")
            .WithSummary("Updates a trip and its route.")
            .RequireValidAntiforgeryToken();

        group.MapGet(
            "/",
            async (
                ClaimsPrincipal user,
                TripService tripService,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var trips = await tripService.ListAsync(viewer, cancellationToken);

                IResult result = Results.Ok(trips);
                return result;
            })
            .WithName("ListTrips")
            .WithSummary("Lists the trips the signed-in account is a member of, ordered by start date and then by name.");

        group.MapGet(
            "/{id:guid}",
            async (
                Guid id,
                ClaimsPrincipal user,
                TripService tripService,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var trip = await tripService.GetAsync(id, viewer, cancellationToken);

                IResult result = trip is null
                    ? Results.Problem(
                        title: "Trip not found",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(trip);

                return result;
            })
            .WithName("GetTripById")
            .WithSummary("Returns a single trip by its identifier.");

        group.MapDelete(
            "/{id:guid}",
            async (
                Guid id,
                ClaimsPrincipal user,
                TripService tripService,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var actor))
                {
                    return Results.Unauthorized();
                }

                var deleted = await tripService.DeleteAsync(id, actor, cancellationToken);

                IResult result = deleted
                    ? Results.NoContent()
                    : Results.Problem(
                        title: "Trip not found",
                        statusCode: StatusCodes.Status404NotFound);

                return result;
            })
            .WithName("DeleteTrip")
            .WithSummary("Deletes a trip and its route.")
            .RequireValidAntiforgeryToken();

        return endpoints;
    }
}
