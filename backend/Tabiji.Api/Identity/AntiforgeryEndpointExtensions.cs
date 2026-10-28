using Microsoft.AspNetCore.Antiforgery;

namespace Tabiji.Api.Identity;

/// <summary>
/// Requires a valid antiforgery token before an endpoint runs.
///
/// The middleware records a verdict rather than ending the request, and only
/// an endpoint that consumes a form is obliged to read it. A JSON endpoint is
/// not, so this asks the question directly and refuses the request itself.
/// The token is validated by <see cref="IAntiforgery"/>; nothing about the
/// token is reimplemented here.
/// </summary>
internal static class AntiforgeryEndpointExtensions
{
    internal static TBuilder RequireValidAntiforgeryToken<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(
                    title: "Invalid antiforgery token",
                    detail: "The request did not carry a valid antiforgery token.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return await next(context);
        });
    }
}
