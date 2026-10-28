using Microsoft.AspNetCore.Identity;

namespace Tabiji.Infrastructure.Persistence.Identity;

/// <summary>
/// The account.
///
/// Deliberately empty of profile data: the first milestone needs a credential
/// and an address, and Identity's own columns already hold both. A name, a
/// picture or a preference goes here when a screen needs it, not before.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>;
