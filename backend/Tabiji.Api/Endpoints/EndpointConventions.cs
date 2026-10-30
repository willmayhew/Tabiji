namespace Tabiji.Api.Endpoints;

internal static class EndpointConventions
{
    /// <summary>
    /// Marks every response of a group as uncacheable. An invitation response
    /// may carry a bearer link, and a shared cache must never keep it.
    /// </summary>
    internal static TBuilder WithNoStore<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
    }
}
