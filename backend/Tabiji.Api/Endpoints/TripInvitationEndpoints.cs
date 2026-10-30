using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Tabiji.Api.Identity;
using Tabiji.Application.Membership;

namespace Tabiji.Api.Endpoints;

/// <summary>
/// The invitations of one trip, as its owners manage them.
///
/// Only an owner may read or change this list. The development build hands
/// back a link instead of sending an email; outside Development, with email
/// delivery still unconfigured, creation is refused rather than leaving an
/// invitation nobody can receive.
/// </summary>
public static class TripInvitationEndpoints
{
    private const string PublicBaseUrlKey = "Invitations:PublicBaseUrl";
    private const string DefaultPublicBaseUrl = "http://localhost:4200";

    public static IEndpointRouteBuilder MapTripInvitationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/trips/{tripId:guid}/invitations")
            .WithTags("Trip invitations")
            .RequireAuthorization()
            .WithNoStore();

        group.MapGet(
            "/",
            async (
                Guid tripId,
                ClaimsPrincipal user,
                InvitationService invitations,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var viewer))
                {
                    return Results.Unauthorized();
                }

                var result = await invitations.ListForTripAsync(tripId, viewer, cancellationToken);

                return result.Outcome switch
                {
                    InvitationListOutcome.Listed => Results.Ok(result.Invitations),
                    InvitationListOutcome.Forbidden => CollaborationProblems.OnlyAnOwner(),
                    _ => CollaborationProblems.TripNotFound(),
                };
            })
            .WithName("ListTripInvitations")
            .WithSummary("Lists a trip's invitations. Only an owner may do this.");

        group.MapPost(
            "/",
            async (
                Guid tripId,
                CreateInvitationRequest request,
                ClaimsPrincipal user,
                InvitationService invitations,
                IWebHostEnvironment environment,
                IConfiguration configuration,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var actor))
                {
                    return Results.Unauthorized();
                }

                // The product has no email provider yet. In Development the
                // link is returned so the flow can be used; everywhere else
                // the request is refused rather than leaving an invitation
                // nobody can receive.
                var deliveryEnabled = configuration.GetValue("Email:DeliveryEnabled", false);

                if (deliveryEnabled || !environment.IsDevelopment())
                {
                    return CollaborationProblems.EmailDeliveryUnavailable();
                }

                CreatedInvitation created;

                try
                {
                    created = await invitations.CreateAsync(
                        tripId,
                        actor,
                        request.Email,
                        cancellationToken);
                }
                catch (ArgumentException exception)
                {
                    return CollaborationProblems.InvalidInvitation(exception.Message);
                }

                if (created is { Outcome: InvitationCreateOutcome.Created, Invitation: not null })
                {
                    var invitation = created.Invitation;
                    var response = new InvitationResponse(
                        invitation.Id,
                        invitation.TripId,
                        invitation.Email,
                        invitation.Role,
                        invitation.Status,
                        invitation.CreatedAt,
                        invitation.ExpiresAt,
                        invitation.InvitedByEmail,
                        BuildInvitationUrl(configuration, created.Token));

                    return Results.Created(
                        $"/api/trips/{tripId}/invitations/{invitation.Id}",
                        response);
                }

                return created.Outcome switch
                {
                    InvitationCreateOutcome.AlreadyMember => CollaborationProblems.Conflict(
                        "Already a member of this trip"),

                    InvitationCreateOutcome.AlreadyInvited => CollaborationProblems.Conflict(
                        "An invitation is already pending for that address"),

                    InvitationCreateOutcome.Forbidden => CollaborationProblems.OnlyAnOwner(),

                    _ => CollaborationProblems.TripNotFound(),
                };
            })
            .WithName("CreateTripInvitation")
            .WithSummary("Invites an address to a trip. Only an owner may do this.")
            .RequireValidAntiforgeryToken()
            .RequireRateLimiting(IdentitySetup.InvitationRateLimitPolicyName);

        group.MapDelete(
            "/{invitationId:guid}",
            async (
                Guid tripId,
                Guid invitationId,
                ClaimsPrincipal user,
                InvitationService invitations,
                CancellationToken cancellationToken) =>
            {
                if (!CurrentUser.TryGetId(user, out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await invitations.RevokeAsync(
                    tripId,
                    actor,
                    invitationId,
                    cancellationToken);

                return result.Outcome switch
                {
                    InvitationActionOutcome.Changed => Results.NoContent(),
                    InvitationActionOutcome.Forbidden => CollaborationProblems.OnlyAnOwner(),
                    _ => CollaborationProblems.InvitationNotFound(),
                };
            })
            .WithName("RevokeTripInvitation")
            .WithSummary("Revokes a pending invitation. Only an owner may do this.")
            .RequireValidAntiforgeryToken();

        return endpoints;
    }

    private static string? BuildInvitationUrl(IConfiguration configuration, string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var baseUrl = configuration[PublicBaseUrlKey];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultPublicBaseUrl;
        }

        return $"{baseUrl.TrimEnd('/')}/invite#token={Uri.EscapeDataString(token)}";
    }
}
