namespace Tabiji.Application.Membership;

/// <summary>
/// A token that is unknown, or belongs to another address, is
/// <see cref="NotFound"/> — the two must not be distinguishable.
/// </summary>
public enum InvitationPreviewOutcome
{
    Found = 0,
    NotFound = 1
}
