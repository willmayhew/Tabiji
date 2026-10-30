namespace Tabiji.Domain.Invitations;

/// <summary>
/// Where an invitation is in its life.
///
/// <see cref="Expired"/> is a stored state as well as a computed one: a write
/// that meets a pending invitation past its expiry records it, so the partial
/// unique index that blocks duplicate pending invitations is freed. A read can
/// present the same status without writing.
/// </summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Revoked = 3,
    Expired = 4
}
