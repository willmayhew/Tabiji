using Microsoft.Extensions.Configuration;

namespace Tabiji.Api.Endpoints;

/// <summary>
/// Builds the link an invitation is opened with.
///
/// The raw token goes in the URL fragment, so it is never sent to the server
/// with the page request and never lands in an access log. One setting names
/// the front end the link points at; the development default is the Angular
/// dev server.
/// </summary>
public sealed class InvitationUrlBuilder(IConfiguration configuration)
{
    public const string PublicBaseUrlKey = "Invitations:PublicBaseUrl";

    private const string DefaultPublicBaseUrl = "http://localhost:4200";

    public string Build(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var baseUrl = configuration[PublicBaseUrlKey];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultPublicBaseUrl;
        }

        return $"{baseUrl.TrimEnd('/')}/invite#token={Uri.EscapeDataString(token)}";
    }
}
