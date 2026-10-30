using System.Security.Claims;
using Tabiji.Api.Identity;
using Tabiji.Application.Membership;

namespace Tabiji.Api.Endpoints;

/// <summary>
/// The invitations waiting for the signed-in account.
///
/// The token travels in the body, never in the path, so it cannot be read from
/// an access log. A token that is unknown, expired, or addressed to somebody
/// else is answered without revealing which of those it is.
/// </summary>
public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/invitations")
            .WithTags("Invitations")
            .RequireAuthorization()
            .WithNoStore();

        group.MapGet(
            "/",
            async (
                ClaimsPrincipal user,
                InvitationService invitations,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var pending = await invitations.ListForRecipientAsync(viewer, cancellationToken);

                return Results.Ok(pending);
            })
            .WithName("ListMyInvitations")
            .WithSummary("Lists the pending invitations addressed to the signed-in account.");

        group.MapPost(
            "/preview",
            async (
                InvitationTokenRequest request,
                ClaimsPrincipal user,
                InvitationService invitations,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var result = await invitations.PreviewAsync(
                    request.Token,
                    viewer,
                    cancellationToken);

                return result is { Outcome: InvitationPreviewOutcome.Found, Preview: not null }
                    ? Results.Ok(result.Preview)
                    : CollaborationProblems.InvitationNotFound();
            })
            .WithName("PreviewInvitation")
            .WithSummary("Shows an invitation to the account it names.")
            .RequireValidAntiforgeryToken();

        group.MapPost(
            "/accept",
            async (
                InvitationTokenRequest request,
                ClaimsPrincipal user,
                InvitationService invitations,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var result = await invitations.AcceptAsync(
                    request.Token,
                    viewer,
                    cancellationToken);

                return MapAccept(result);
            })
            .WithName("AcceptInvitation")
            .WithSummary("Accepts an invitation addressed to the signed-in account.")
            .RequireValidAntiforgeryToken();

        group.MapPost(
            "/decline",
            async (
                InvitationTokenRequest request,
                ClaimsPrincipal user,
                InvitationService invitations,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var result = await invitations.DeclineAsync(
                    request.Token,
                    viewer,
                    cancellationToken);

                return MapAction(result);
            })
            .WithName("DeclineInvitation")
            .WithSummary("Declines an invitation addressed to the signed-in account.")
            .RequireValidAntiforgeryToken();

        group.MapPost(
            "/{invitationId:guid}/accept",
            async (
                Guid invitationId,
                ClaimsPrincipal user,
                InvitationService invitations,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var result = await invitations.AcceptByIdAsync(
                    invitationId,
                    viewer,
                    cancellationToken);

                return MapAccept(result);
            })
            .WithName("AcceptInvitationById")
            .WithSummary("Accepts an invitation by its identifier, for the trips page.")
            .RequireValidAntiforgeryToken();

        group.MapPost(
            "/{invitationId:guid}/decline",
            async (
                Guid invitationId,
                ClaimsPrincipal user,
                InvitationService invitations,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var result = await invitations.DeclineByIdAsync(
                    invitationId,
                    viewer,
                    cancellationToken);

                return MapAction(result);
            })
            .WithName("DeclineInvitationById")
            .WithSummary("Declines an invitation by its identifier, for the trips page.")
            .RequireValidAntiforgeryToken();

        return endpoints;
    }

    private static IResult MapAccept(AcceptInvitationResult result) => result.Outcome switch
    {
        InvitationAcceptOutcome.Accepted when result.Accepted is not null => Results.Ok(result.Accepted),
        InvitationAcceptOutcome.Expired => CollaborationProblems.Conflict("Invitation has expired"),
        InvitationAcceptOutcome.AlreadyUsed => CollaborationProblems.Conflict(
            "Invitation has already been used"),
        InvitationAcceptOutcome.Declined => CollaborationProblems.Conflict("Invitation was declined"),
        InvitationAcceptOutcome.Revoked => CollaborationProblems.Conflict("Invitation was revoked"),
        _ => CollaborationProblems.InvitationNotFound(),
    };

    private static IResult MapAction(InvitationActionResult result) => result.Outcome switch
    {
        InvitationActionOutcome.Changed => Results.NoContent(),
        InvitationActionOutcome.Expired => CollaborationProblems.Conflict("Invitation has expired"),
        InvitationActionOutcome.AlreadyUsed => CollaborationProblems.Conflict(
            "Invitation has already been used"),
        InvitationActionOutcome.Declined => CollaborationProblems.Conflict("Invitation was declined"),
        InvitationActionOutcome.Revoked => CollaborationProblems.Conflict("Invitation was revoked"),
        _ => CollaborationProblems.InvitationNotFound(),
    };
}
