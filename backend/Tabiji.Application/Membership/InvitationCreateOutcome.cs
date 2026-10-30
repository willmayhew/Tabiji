namespace Tabiji.Application.Membership;

/// <summary>
/// What happened when an invitation was issued.
///
/// The address having an account is deliberately not a separate outcome: an
/// invitation works either way, and the response must not become a way to
/// discover who has registered.
/// </summary>
public enum InvitationCreateOutcome
{
    Created = 0,
    NotFound = 1,
    Forbidden = 2,
    AlreadyMember = 3,
    AlreadyInvited = 4
}
