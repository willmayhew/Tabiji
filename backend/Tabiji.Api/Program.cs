using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Tabiji.Api.Endpoints;
using Tabiji.Api.Health;
using Tabiji.Api.Identity;
using Tabiji.Application;
using Tabiji.Infrastructure;

const string CorsPolicyName = "frontend";
const string DefaultAllowedOrigin = "http://localhost:4200";
const string EmailDeliveryEnabledKey = "Email:DeliveryEnabled";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTabijiIdentity(builder.Environment);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
    .WithOrigins(AllowedOrigins(builder.Configuration))
    .AllowAnyHeader()
    .AllowAnyMethod()
    // The authentication cookie only travels on a cross-origin request that
    // allows credentials. The development setup avoids the question with its
    // same-origin proxy; a split-origin deployment needs this, and the origin
    // list is explicit so a wildcard can never be combined with credentials.
    .AllowCredentials()));

// TLS ends at the reverse proxy, so the scheme and the client address have to
// be read from the headers it forwards. Only a proxy on this machine is
// trusted by default, which is the shape of the single-origin deployment.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

app.UseCors(CorsPolicyName);
app.UseForwardedHeaders();

// The account routes the product does not use do not exist. Answered before
// authentication runs, so no credential is needed to learn that, and the
// fallback policy below never has to make an exception for them.
app.Use(async (context, next) =>
{
    if (UnusedIdentityEndpoints.IsUnused(context.Request.Path))
    {
        await Results.Problem(
            title: "Not found",
            statusCode: StatusCodes.Status404NotFound).ExecuteAsync(context);

        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

// Confirmation and password recovery need somewhere to deliver a link. Until
// that exists, say so instead of answering 200 and dropping the message.
if (!builder.Configuration.GetValue(EmailDeliveryEnabledKey, false))
{
    app.Use(async (context, next) =>
    {
        if (EmailDependentEndpoints.IsEmailDependent(context.Request.Path))
        {
            await Results.Problem(
                title: "Email delivery is not configured",
                detail: "Account confirmation and password recovery are unavailable on this deployment.",
                statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);

            return;
        }

        await next();
    });
}

app.MapOpenApi()
    .AllowAnonymous();

app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = HealthResponseWriter.WriteAsync
    })
    .AllowAnonymous()
    .WithTags("Health")
    .WithSummary("Liveness probe. Does not touch the database.");

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = HealthResponseWriter.WriteAsync
    })
    .AllowAnonymous()
    .WithTags("Health")
    .WithSummary("Readiness probe. Verifies that the database is reachable.");

app.MapTripEndpoints();
app.MapSavedPlaceEndpoints();
app.MapAuthEndpoints();

app.Run();

static string[] AllowedOrigins(IConfiguration configuration)
{
    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

    return origins is { Length: > 0 } ? origins : [DefaultAllowedOrigin];
}

public partial class Program
{
}
