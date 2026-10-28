namespace Tabiji.Api.Endpoints;

/// <summary>
/// Whether the request carries a session, and who it belongs to.
///
/// Reported as a body rather than a 401 so the shell can ask on every load
/// without a failed request in the console.
/// </summary>
public sealed record SessionResponse(bool Authenticated, SessionUserResponse? User);
