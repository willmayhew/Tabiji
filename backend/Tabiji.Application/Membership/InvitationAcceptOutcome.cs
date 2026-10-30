namespace Tabiji.Application.Membership;

/// <summary>
/// What happened when a recipient tried to accept.
///
/// The terminal states are only reported to the address the invitation names;
/// anyone else sees <see cref="NotFound"/>.
/// </summary>
public enum InvitationAcceptOutcome
{
    Accepted = 0,
    NotFound = 1,
    Expired = 2,
    AlreadyUsed = 3,
    Declined = 4,
    Revoked = 5
}
