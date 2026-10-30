using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Tabiji.Infrastructure.Persistence;
using Tabiji.Infrastructure.Persistence.Identity;

namespace Tabiji.Api.Identity;

/// <summary>
/// The accounts, registered where the rest of the host is composed.
///
/// Identity owns the credential, the hash, the lockout counter and the cookie.
/// Nothing here reimplements any of that: it chooses which of Identity's own
/// knobs Tabiji turns, and it keeps the accounts out of the domain.
/// </summary>
public static class IdentitySetup
{
    public const string RateLimitPolicyName = "auth";

    private const string UnknownAddress = "unknown";

    public static IServiceCollection AddTabijiIdentity(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        var isDevelopment = environment.IsDevelopment();

        services
            .AddIdentityApiEndpoints<AppUser>(options =>
            {
                // Sign-in is by address, so two accounts may not share one.
                options.User.RequireUniqueEmail = true;

                // There is no way to deliver an email yet. Registering a usable
                // account immediately is the honest behaviour; the four routes
                // that would need an email answer 503 instead.
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedAccount = false;

                // Long enough to matter, short of the character-class ritual
                // that makes a passphrase harder to remember than to guess.
                options.Password.RequiredLength = 10;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<TabijiDbContext>();

        // Every endpoint demands an account unless it deliberately says
        // otherwise, so a route added next year is protected before anybody
        // remembers to protect it. The anonymous surface is small and named:
        // the account routes, the two probes and the OpenAPI document.
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = ".Tabiji.Auth";

            // The browser never has to read it, so it cannot leak to a script.
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = isDevelopment
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // ASP.NET Core 10 already answers 401 for an API endpoint under
            // cookie authentication. Stating it here means a future default
            // cannot quietly turn a 401 into a redirect at a login page this
            // API does not have. The events object already carries the
            // security-stamp validator, so it is amended rather than replaced.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = ".Tabiji.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = isDevelopment
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicyName, context =>
            {
                var path = context.Request.Path.Value ?? string.Empty;

                // Only the routes that check a credential get the tight bucket.
                // The session probe runs on every page load and must not.
                var (permitLimit, window) = path is "/api/auth/login"
                    or "/api/auth/register"
                    or "/api/auth/forgotPassword"
                    or "/api/auth/resetPassword"
                        ? (10, TimeSpan.FromMinutes(1))
                        : (120, TimeSpan.FromMinutes(1));

                return RateLimitPartition.GetFixedWindowLimiter(
                    (context.Connection.RemoteIpAddress?.ToString() ?? UnknownAddress, path),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }
}
