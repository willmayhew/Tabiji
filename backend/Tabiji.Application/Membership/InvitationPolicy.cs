namespace Tabiji.Application.Membership;

/// <summary>
/// How long an invitation remains acceptable.
///
/// It is a plain value rather than an options type so the application layer
/// keeps its single dependency on the dependency-injection abstractions. The
/// composition root reads the setting.
/// </summary>
public sealed record InvitationPolicy(TimeSpan Lifetime);
