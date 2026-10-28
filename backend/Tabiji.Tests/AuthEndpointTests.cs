using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tabiji.Api.Endpoints;
using Tabiji.Infrastructure.Persistence;

namespace Tabiji.Tests;

/// <summary>
/// The account routes, end to end through the real host and a real store.
///
/// Identity's password hasher, lockout counter, cookie handler and user
/// validator are all in play, so these are the tests that would catch a
/// configuration mistake the model tests cannot see. The suite needs a
/// database for them, and skips them when LocalDB is not there, exactly as the
/// repository tests do.
/// </summary>
public sealed class AuthEndpointTests : IAsyncLifetime, IDisposable
{
    private const string Email = "traveller@example.com";
    private const string Password = "CorrectHorse42";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AuthApiFactory _factory = new();

    public async Task InitializeAsync() => await ResetDatabaseAsync();

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        SqlConnection.ClearAllPools();

        await using var context = new TabijiDbContext(Options());
        await context.Database.EnsureDeletedAsync();

        Dispose();
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [DatabaseFact]
    public async Task Session_IsAnonymousWithoutACookie()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/session");

        // A probe, not a protected resource: asking is always a 200, so the
        // shell never has to treat "signed out" as a failure.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions);

        Assert.NotNull(session);
        Assert.False(session.Authenticated);
        Assert.Null(session.User);
    }

    [DatabaseFact]
    public async Task Register_CreatesTheAccountButDoesNotSignItIn()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new Credentials(Email, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session", JsonOptions);

        Assert.NotNull(session);
        Assert.False(session.Authenticated);
    }

    [DatabaseFact]
    public async Task Register_RefusesAnAddressThatAlreadyHasAnAccount()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new Credentials(Email, "AnotherPassword42"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(JsonOptions);

        Assert.NotNull(problem);
        Assert.NotNull(problem.Errors);
        Assert.True(problem.Errors.ContainsKey("DuplicateEmail"));
    }

    [DatabaseFact]
    public async Task Register_RefusesAPasswordBelowTheConfiguredLength()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new Credentials("short@example.com", "Short1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(JsonOptions);

        Assert.NotNull(problem);
        Assert.NotNull(problem.Errors);
        Assert.True(problem.Errors.ContainsKey("PasswordTooShort"));
    }

    [DatabaseFact]
    public async Task Login_IssuesAnHttpOnlyLaxCookie()
    {
        // Cookies are handled by hand here so the header itself can be read.
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false });

        await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login?useCookies=true",
            new Credentials(Email, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.Contains(".Tabiji.Auth=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseFact]
    public async Task Login_ThenSession_ReportsTheSignedInUser()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);
        await LoginAsync(client);

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session", JsonOptions);

        Assert.NotNull(session);
        Assert.True(session.Authenticated);
        Assert.NotNull(session.User);
        Assert.Equal(Email, session.User.Email);
        Assert.NotEqual(Guid.Empty, session.User.Id);
    }

    [DatabaseFact]
    public async Task Login_RefusesAWrongPasswordAndLeavesTheSessionAnonymous()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login?useCookies=true",
            new Credentials(Email, "WrongPassword42"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session", JsonOptions);

        Assert.NotNull(session);
        Assert.False(session.Authenticated);
    }

    [DatabaseFact]
    public async Task Logout_ClearsTheSession()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);
        await LoginAsync(client);

        var response = await client.SendAsync(LogoutRequest(await CsrfTokenAsync(client)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session", JsonOptions);

        Assert.NotNull(session);
        Assert.False(session.Authenticated);
    }

    [DatabaseFact]
    public async Task Logout_RefusesARequestThatDoesNotCarryTheToken()
    {
        using var client = _factory.CreateClient();
        await RegisterAsync(client);
        await LoginAsync(client);

        // The cookie alone is not proof that this page sent the request.
        var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [DatabaseFact]
    public async Task Logout_WithoutASessionStillSucceedsWithAValidToken()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(LogoutRequest(await CsrfTokenAsync(client)));

        // A session that expired while the page was open must still sign out.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [DatabaseFact]
    public async Task Login_IsRateLimitedAfterTenAttemptsInAMinute()
    {
        using var client = _factory.CreateClient();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var allowed = await client.PostAsJsonAsync(
                "/api/auth/login?useCookies=true",
                new Credentials("nobody@example.com", "WrongPassword42"));

            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var limited = await client.PostAsJsonAsync(
            "/api/auth/login?useCookies=true",
            new Credentials("nobody@example.com", "WrongPassword42"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [DatabaseFact]
    public async Task Session_IsNotLimitedAtTheCredentialRate()
    {
        using var client = _factory.CreateClient();

        // A page load asks this question; it must not spend the login budget.
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            var response = await client.GetAsync("/api/auth/session");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static DbContextOptions<TabijiDbContext> Options() =>
        new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(AuthApiFactory.ConnectionString)
            .Options;

    private static async Task ResetDatabaseAsync()
    {
        SqlConnection.ClearAllPools();

        await using var context = new TabijiDbContext(Options());

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    private static async Task RegisterAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new Credentials(Email, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login?useCookies=true",
            new Credentials(Email, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> CsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/auth/csrf", JsonOptions);

        Assert.NotNull(response);

        return response.Token;
    }

    private static HttpRequestMessage LogoutRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", token);

        return request;
    }

    private sealed record Credentials(string Email, string Password);

    private sealed record ValidationProblem(string? Title, int? Status, Dictionary<string, string[]>? Errors);
}

/// <summary>
/// The routes that need somewhere to send an email.
///
/// They are refused before the account code runs, so this needs no database:
/// what is being checked is that the deployment says what it cannot do rather
/// than answering as if a message had been sent.
/// </summary>
public sealed class EmailDependentEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TabijiWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("GET", "/api/auth/confirmEmail")]
    [InlineData("POST", "/api/auth/resendConfirmationEmail")]
    [InlineData("POST", "/api/auth/forgotPassword")]
    [InlineData("POST", "/api/auth/resetPassword")]
    public async Task Route_Answers503UntilEmailDeliveryIsConfigured(string method, string path)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { email = "anyone@example.com" });
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ReferenceProblem>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal("Email delivery is not configured", problem.Title);
    }

    private sealed record ReferenceProblem(string? Title, string? Detail, int? Status);
}
