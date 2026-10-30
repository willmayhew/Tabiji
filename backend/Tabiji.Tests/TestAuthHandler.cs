using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tabiji.Tests;

/// <summary>
/// Stands in for the authentication cookie, so an endpoint test can be a
/// particular account without a database.
///
/// The account is named by a request header. A request without the header is
/// anonymous, exactly as a request without the cookie is, so the authorization
/// under test — the fallback policy, the group policy, the 401 — is the real
/// thing rather than a stub that always succeeds.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "Test";

    internal const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(header.ToString(), out var userId) || userId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.Fail("The test account header is not an account id."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, $"test-{userId:D}@example.com"),
                new Claim(ClaimTypes.Email, $"test-{userId:D}@example.com")
            ],
            SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
