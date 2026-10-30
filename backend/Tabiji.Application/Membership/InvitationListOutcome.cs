namespace Tabiji.Application.Membership;

/// <summary>
/// Whether the caller may see a trip's invitations. Pending invitations carry
/// addresses, so only an owner may read them.
/// </summary>
public enum InvitationListOutcome
{
    Listed = 0,
    NotFound = 1,
    Forbidden = 2
}
