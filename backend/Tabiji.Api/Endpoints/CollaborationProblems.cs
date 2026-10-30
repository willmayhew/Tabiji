namespace Tabiji.Api.Endpoints;

/// <summary>
/// The ProblemDetails a collaboration route answers with.
///
/// A trip the caller cannot reach and a trip that does not exist are the same
/// 404. A member whose role is not enough is 403, because the trip is not
/// hidden from them. A conflict is 409, because it is a fact they are entitled
/// to know.
/// </summary>
internal static class CollaborationProblems
{
    internal static IResult TripNotFound() => Results.Problem(
        title: "Trip not found",
        statusCode: StatusCodes.Status404NotFound);

    internal static IResult MemberNotFound() => Results.Problem(
        title: "Member not found",
        statusCode: StatusCodes.Status404NotFound);

    internal static IResult InvitationNotFound() => Results.Problem(
        title: "Invitation not found",
        statusCode: StatusCodes.Status404NotFound);

    internal static IResult OnlyAnOwner() => Results.Problem(
        title: "Only an owner can manage this trip",
        statusCode: StatusCodes.Status403Forbidden);

    internal static IResult LastOwner() => Results.Problem(
        title: "A trip must keep at least one owner",
        statusCode: StatusCodes.Status409Conflict);

    internal static IResult InvalidRole() => Results.Problem(
        title: "Invalid role",
        detail: "A member may be an owner or an editor.",
        statusCode: StatusCodes.Status400BadRequest);

    internal static IResult Conflict(string title, string? detail = null) => Results.Problem(
        title: title,
        detail: detail,
        statusCode: StatusCodes.Status409Conflict);

    internal static IResult InvalidInvitation(string detail) => Results.Problem(
        title: "Invalid invitation",
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest);

    internal static IResult EmailDeliveryUnavailable() => Results.Problem(
        title: "Email delivery is not configured",
        detail: "Invitations cannot be sent on this deployment until an email provider exists.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
