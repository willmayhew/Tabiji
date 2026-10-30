using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tabiji.Api.Identity;

namespace Tabiji.Tests;

/// <summary>
/// How the account cookie and the antiforgery token are configured.
///
/// These are the settings that decide whether a session survives a reload,
/// whether a script can read the cookie, and whether a plain-HTTP development
/// server can hold one at all. They are read from the real host in each
/// environment rather than from a copy of the configuration.
/// </summary>
public sealed class IdentitySetupTests
{
    private static CookieAuthenticationOptions ApplicationCookie(IServiceProvider services) =>
        services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

    private static async Task<int> RedirectStatusAsync(
        Func<RedirectContext<CookieAuthenticationOptions>, Task> handler,
        CookieAuthenticationOptions options)
    {
        var context = new DefaultHttpContext();
        var scheme = new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            displayName: null,
            typeof(CookieAuthenticationHandler));

        await handler(new RedirectContext<CookieAuthenticationOptions>(
            context,
            scheme,
            options,
            new AuthenticationProperties(),
            redirectUri: "/api/auth/session"));

        return context.Response.StatusCode;
    }

    [Fact]
    public void ApplicationCookie_CannotBeReadByScriptAndIsNotSentAcrossSites()
    {
        using var factory = new EnvironmentApiFactory(Environments.Production);

        var options = ApplicationCookie(factory.Services);

        Assert.Equal(".Tabiji.Auth", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
    }

    [Fact]
    public void ApplicationCookie_IsSecureOutsideDevelopment()
    {
        using var factory = new EnvironmentApiFactory(Environments.Production);

        Assert.Equal(CookieSecurePolicy.Always, ApplicationCookie(factory.Services).Cookie.SecurePolicy);
    }

    [Fact]
    public void ApplicationCookie_FollowsTheRequestSchemeOnThePlainHttpDevelopmentServer()
    {
        using var factory = new EnvironmentApiFactory(Environments.Development);

        // The dev server is http on port 4200 and proxies over http. A cookie
        // marked Secure would never be stored, so the studio could not sign in.
        Assert.Equal(
            CookieSecurePolicy.SameAsRequest,
            ApplicationCookie(factory.Services).Cookie.SecurePolicy);
    }

    [Fact]
    public void ApplicationCookie_SlidesForFourteenDays()
    {
        using var factory = new EnvironmentApiFactory(Environments.Production);

        var options = ApplicationCookie(factory.Services);

        Assert.Equal(TimeSpan.FromDays(14), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
    }

    [Fact]
    public async Task ApplicationCookie_AnswersWithAStatusInsteadOfRedirectingToALoginPage()
    {
        using var factory = new EnvironmentApiFactory(Environments.Production);

        var options = ApplicationCookie(factory.Services);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            await RedirectStatusAsync(options.Events.OnRedirectToLogin, options));

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            await RedirectStatusAsync(options.Events.OnRedirectToAccessDenied, options));
    }

    [Fact]
    public void IdentityOptions_RequireOneAccountPerAddressAndNoConfirmationYet()
    {
        using var factory = new EnvironmentApiFactory(Environments.Production);

        var options = factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(options.User.RequireUniqueEmail);
        Assert.False(options.SignIn.RequireConfirmedEmail);
        Assert.False(options.SignIn.RequireConfirmedAccount);
        Assert.Equal(10, options.Password.RequiredLength);
        Assert.Equal(4, options.Password.RequiredUniqueChars);
        Assert.False(options.Password.RequireNonAlphanumeric);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
    }

    [Fact]
    public void AntiforgeryToken_TravelsInTheHeaderTheClientEchoes()
    {
        using var factory = new EnvironmentApiFactory(Environments.Production);

        var options = factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.Equal("X-XSRF-TOKEN", options.HeaderName);
        Assert.Equal(".Tabiji.Antiforgery", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
    }

    [Fact]
    public void Authorization_DeniesByDefault()
    {
        using var factory = new EnvironmentApiFactory(Environments.Production);

        var options = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        Assert.NotNull(options.FallbackPolicy);

        // An endpoint that says nothing demands an account. That is what makes
        // a route added later protected before anybody remembers to protect it.
        Assert.Contains(
            options.FallbackPolicy!.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }
}
