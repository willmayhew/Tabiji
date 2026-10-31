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
public static partial class TripInvitationEndpoints
{
    private const string LogCategory = "Tabiji.Api.Endpoints.TripInvitationEndpoints";

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
                InvitationUrlBuilder invitationUrls,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var logger = loggerFactory.CreateLogger(LogCategory);

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
                    InvitationDeliveryUnavailable(logger, tripId);

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

                    InvitationCreated(logger, invitation.Id, tripId);

                    var response = new InvitationResponse(
                        invitation.Id,
                        invitation.TripId,
                        invitation.Email,
                        invitation.Role,
                        invitation.Status,
                        invitation.CreatedAt,
                        invitation.ExpiresAt,
                        invitation.InvitedByEmail,
                        created.Token is null ? null : invitationUrls.Build(created.Token));

                    return Results.Created(
                        $"/api/trips/{tripId}/invitations/{invitation.Id}",
                        response);
                }

                InvitationRefused(logger, tripId, created.Outcome);

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
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var logger = loggerFactory.CreateLogger(LogCategory);

                if (!CurrentUser.TryGetId(user, out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await invitations.RevokeAsync(
                    tripId,
                    actor,
                    invitationId,
                    cancellationToken);

                if (result.Outcome is InvitationActionOutcome.Changed)
                {
                    InvitationRevoked(logger, invitationId, tripId);

                    return Results.NoContent();
                }

                InvitationRevokeRefused(logger, invitationId, tripId, result.Outcome);

                return result.Outcome switch
                {
                    InvitationActionOutcome.Forbidden => CollaborationProblems.OnlyAnOwner(),
                    _ => CollaborationProblems.InvitationNotFound(),
                };
            })
            .WithName("RevokeTripInvitation")
            .WithSummary("Revokes a pending invitation. Only an owner may do this.")
            .RequireValidAntiforgeryToken();

        return endpoints;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Invitation {InvitationId} created for trip {TripId}.")]
    private static partial void InvitationCreated(ILogger logger, Guid invitationId, Guid tripId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Refused to invite to trip {TripId}: {Outcome}.")]
    private static partial void InvitationRefused(
        ILogger logger,
        Guid tripId,
        InvitationCreateOutcome outcome);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Refused to invite to trip {TripId}: email delivery is not configured.")]
    private static partial void InvitationDeliveryUnavailable(ILogger logger, Guid tripId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Invitation {InvitationId} revoked from trip {TripId}.")]
    private static partial void InvitationRevoked(ILogger logger, Guid invitationId, Guid tripId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Refused to revoke invitation {InvitationId} from trip {TripId}: {Outcome}.")]
    private static partial void InvitationRevokeRefused(
        ILogger logger,
        Guid invitationId,
        Guid tripId,
        InvitationActionOutcome outcome);
}
