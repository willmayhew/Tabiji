namespace Tabiji.Api.Endpoints;

/// <summary>
/// A request token, to be echoed in the <c>X-XSRF-TOKEN</c> header on anything
/// that changes state.
/// </summary>
public sealed record CsrfTokenResponse(string Token);
