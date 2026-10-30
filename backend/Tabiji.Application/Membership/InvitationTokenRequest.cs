namespace Tabiji.Application.Membership;

/// <summary>
/// The token carried by an invitation link. It travels in the body rather than
/// the path so it never reaches an access log.
/// </summary>
public sealed record InvitationTokenRequest(string? Token);
