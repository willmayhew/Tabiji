using System.Security.Claims;
using Tabiji.Api.Identity;
using Tabiji.Application.Membership;
using Tabiji.Domain.Membership;

namespace Tabiji.Api.Endpoints;

/// <summary>
/// Who is on a trip, and the owner's ability to change that.
///
/// Any member may see the list. Only an owner may change a role or remove
/// someone; an editor is a member, so the answer is 403 rather than the 404 a
/// stranger receives.
/// </summary>
public static class TripMemberEndpoints
{
    public static IEndpointRouteBuilder MapTripMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/trips/{tripId:guid}/members")
            .WithTags("Trip members")
            .RequireAuthorization();

        group.MapGet(
            "/",
            async (
                Guid tripId,
                ClaimsPrincipal user,
                TripMembershipService members,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var result = await members.ListAsync(tripId, viewer, cancellationToken);

                return result is null ? CollaborationProblems.TripNotFound() : Results.Ok(result);
            })
            .WithName("ListTripMembers")
            .WithSummary("Lists the people on a trip and the caller's own role.");

        group.MapPatch(
            "/{memberId:guid}",
            async (
                Guid tripId,
                Guid memberId,
                ChangeMemberRoleRequest request,
                ClaimsPrincipal user,
                TripMembershipService members,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var actor))
                {
                    return Results.Unauthorized();
                }

                if (memberId == Guid.Empty)
                {
                    return CollaborationProblems.MemberNotFound();
                }

                var result = await members.ChangeRoleAsync(
                    tripId,
                    actor,
                    UserId.Create(memberId),
                    request.Role,
                    cancellationToken);

                return result.Outcome switch
                {
                    MembershipChangeOutcome.Changed => Results.Ok(result.Members),
                    MembershipChangeOutcome.NotFound => CollaborationProblems.TripNotFound(),
                    MembershipChangeOutcome.MemberNotFound => CollaborationProblems.MemberNotFound(),
                    MembershipChangeOutcome.Forbidden => CollaborationProblems.OnlyAnOwner(),
                    MembershipChangeOutcome.LastOwner => CollaborationProblems.LastOwner(),
                    MembershipChangeOutcome.InvalidRole => CollaborationProblems.InvalidRole(),
                    _ => CollaborationProblems.TripNotFound(),
                };
            })
            .WithName("ChangeTripMemberRole")
            .WithSummary("Changes a member's role. Only an owner may do this, and the last owner cannot be demoted.")
            .RequireValidAntiforgeryToken();

        group.MapDelete(
            "/{memberId:guid}",
            async (
                Guid tripId,
                Guid memberId,
                ClaimsPrincipal user,
                TripMembershipService members,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var actor))
                {
                    return Results.Unauthorized();
                }

                if (memberId == Guid.Empty)
                {
                    return CollaborationProblems.MemberNotFound();
                }

                var outcome = await members.RemoveAsync(
                    tripId,
                    actor,
                    UserId.Create(memberId),
                    cancellationToken);

                return outcome switch
                {
                    MembershipChangeOutcome.Changed => Results.NoContent(),
                    MembershipChangeOutcome.NotFound => CollaborationProblems.TripNotFound(),
                    MembershipChangeOutcome.MemberNotFound => CollaborationProblems.MemberNotFound(),
                    MembershipChangeOutcome.Forbidden => CollaborationProblems.OnlyAnOwner(),
                    MembershipChangeOutcome.LastOwner => CollaborationProblems.LastOwner(),
                    _ => CollaborationProblems.TripNotFound(),
                };
            })
            .WithName("RemoveTripMember")
            .WithSummary("Removes a member. Only an owner may do this, and the last owner cannot be removed.")
            .RequireValidAntiforgeryToken();

        return endpoints;
    }
}
