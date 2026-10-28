namespace Tabiji.Api.Identity;

/// <summary>
/// The account routes that cannot work until something can send an email.
///
/// Account confirmation and password recovery both deliver a link or a code to
/// an address. Tabiji has no mail transport, no provider and no credential for
/// one, so those routes refuse rather than answer 200 and drop the message.
/// </summary>
internal static class EmailDependentEndpoints
{
    private static readonly string[] Paths =
    [
        "/api/auth/confirmEmail",
        "/api/auth/resendConfirmationEmail",
        "/api/auth/forgotPassword",
        "/api/auth/resetPassword"
    ];

    internal static bool IsEmailDependent(PathString path) =>
        Paths.Any(candidate =>
            path.Equals(new PathString(candidate), StringComparison.OrdinalIgnoreCase));
}
