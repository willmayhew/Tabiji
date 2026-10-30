namespace Tabiji.Application.Membership;

/// <summary>
/// The address to invite. Only the address comes from the body: the trip and
/// the caller come from the route and the cookie.
/// </summary>
public sealed record CreateInvitationRequest(string? Email);
