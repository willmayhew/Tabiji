namespace Tabiji.Api.Identity;

/// <summary>
/// The account routes the framework maps but the product does not use.
///
/// <c>MapIdentityApi</c> maps a two-factor surface and a token refresh the SPA
/// never calls. They are cookie-authenticated and carry no antiforgery
/// requirement — a state-changing route protected by <c>SameSite=Lax</c>
/// alone — and a two-factor endpoint nobody links to is only attack surface.
/// They answer 404, which is the truth: this API has no such route.
/// </summary>
internal static class UnusedIdentityEndpoints
{
    private static readonly string[] Paths =
    [
        "/api/auth/manage/2fa",
        "/api/auth/manage/info",
        "/api/auth/refresh"
    ];

    internal static bool IsUnused(PathString path) =>
        Paths.Any(candidate =>
            path.Equals(new PathString(candidate), StringComparison.OrdinalIgnoreCase));
}
