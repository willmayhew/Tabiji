using System.Security.Claims;
using Tabiji.Domain.Membership;

namespace Tabiji.Api.Identity;

/// <summary>
/// The account a request was authenticated as.
///
/// The identifier comes from the cookie's claims, which Identity wrote from the
/// account's own key. It is the only source of an owner: nothing about who a
/// caller is ever comes from the request body.
/// </summary>
internal static class CurrentUser
{
    /// <summary>
    /// Reads the account identifier. A missing or malformed claim means the
    /// request is not usable as an account, even though authorization let it
    /// through.
    /// </summary>
    internal static bool TryGetId(ClaimsPrincipal principal, out UserId userId)
    {
        var identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(identifier, out var value) && value != Guid.Empty)
        {
            userId = UserId.Create(value);
            return true;
        }

        userId = default;
        return false;
    }
}
