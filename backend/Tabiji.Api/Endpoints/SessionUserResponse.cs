namespace Tabiji.Api.Endpoints;

/// <summary>
/// The signed-in account, as the browser is allowed to know it.
///
/// The identifier is the key a trip membership will point at in the next
/// milestone. The address is what the reader typed to sign in.
/// </summary>
public sealed record SessionUserResponse(Guid Id, string Email);
