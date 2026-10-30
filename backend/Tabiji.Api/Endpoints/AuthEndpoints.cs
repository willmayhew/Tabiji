using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Tabiji.Api.Identity;
using Tabiji.Infrastructure.Persistence.Identity;

namespace Tabiji.Api.Endpoints;

/// <summary>
/// The account routes.
///
/// Everything that checks a password or mints a cookie is Identity's own
/// endpoint set; the three routes written here are the ones Identity does not
/// provide: what the browser asks to find out who it is, how it signs out, and
/// where it collects the token that proves a state-changing request came from
/// this page and not another.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/auth")
            .WithTags("Auth")
            .RequireRateLimiting(IdentitySetup.RateLimitPolicyName);

        // register, login, refresh, confirmEmail, resendConfirmationEmail,
        // forgotPassword, resetPassword and manage/*, all under this prefix.
        //
        // They are public by design: registration, sign-in and the email
        // routes carry no cookie yet, so there is nothing to require. The two
        // surfaces that are not public — the token refresh and manage/* —
        // never reach an endpoint: Program.cs answers them 404 before
        // authentication runs.
        group.MapIdentityApi<AppUser>().AllowAnonymous();

        group.MapGet(
            "/session",
            (ClaimsPrincipal user) =>
            {
                var identifier = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = user.FindFirst(ClaimTypes.Email)?.Value;

                IResult result;

                if (Guid.TryParse(identifier, out var userId) && !string.IsNullOrEmpty(email))
                {
                    result = Results.Ok(new SessionResponse(true, new SessionUserResponse(userId, email)));
                }
                else
                {
                    result = Results.Ok(new SessionResponse(false, null));
                }

                return result;
            })
            .AllowAnonymous()
            .WithName("GetSession")
            .WithSummary("Reports whether the request carries a session, and whose.");

        group.MapGet(
            "/csrf",
            (HttpContext context, IAntiforgery antiforgery) =>
            {
                var token = antiforgery.GetAndStoreTokens(context).RequestToken;

                IResult result = Results.Ok(new CsrfTokenResponse(token!));
                return result;
            })
            .AllowAnonymous()
            .WithName("GetCsrfToken")
            .WithSummary("Issues the antiforgery cookie and returns the token to echo in the request header.");

        group.MapPost(
            "/logout",
            async (SignInManager<AppUser> signInManager) =>
            {
                // Clearing a cookie that is already gone is not an error: a
                // session that expired while the page was open still signs out.
                await signInManager.SignOutAsync();

                IResult result = Results.NoContent();
                return result;
            })
            .WithName("Logout")
            .WithSummary("Clears the authentication cookie.")
            // Clearing a cookie that is already gone is not an error, and the
            // token is what proves the request came from the page that holds
            // the cookie. No account is required beyond that.
            .AllowAnonymous()
            .RequireValidAntiforgeryToken();

        return endpoints;
    }
}
