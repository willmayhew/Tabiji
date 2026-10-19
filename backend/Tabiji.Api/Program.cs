using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Tabiji.Api.Endpoints;
using Tabiji.Api.Health;
using Tabiji.Application;
using Tabiji.Infrastructure;

const string CorsPolicyName = "frontend";
const string DefaultAllowedOrigin = "http://localhost:4200";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
    .WithOrigins(AllowedOrigins(builder.Configuration))
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

app.UseCors(CorsPolicyName);

app.MapOpenApi();

app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = HealthResponseWriter.WriteAsync
    })
    .WithTags("Health")
    .WithSummary("Liveness probe. Does not touch the database.");

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = HealthResponseWriter.WriteAsync
    })
    .WithTags("Health")
    .WithSummary("Readiness probe. Verifies that the database is reachable.");

app.MapTripEndpoints();

app.Run();

static string[] AllowedOrigins(IConfiguration configuration)
{
    var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

    return origins is { Length: > 0 } ? origins : [DefaultAllowedOrigin];
}

public partial class Program
{
}
