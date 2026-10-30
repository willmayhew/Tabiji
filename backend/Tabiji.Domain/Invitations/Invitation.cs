using Tabiji.Domain.Membership;

namespace Tabiji.Domain.Invitations;

/// <summary>
/// An offer to join a trip, addressed to one email address.
///
/// It is its own aggregate: a trip can have many invitations, each with its own
/// lifecycle, and an invitation is not a membership until it is accepted. The
/// raw token is never held here — only the hash that lets the API find the row
/// again. The token itself exists in the response that issues it, and nowhere
/// else.
/// </summary>
public sealed class Invitation
{
    public const int TokenHashLength = 32;

    private Invitation()
    {
    }

    private Invitation(
        Guid id,
        Guid tripId,
        EmailAddress email,
        TripMemberRole role,
        byte[] tokenHash,
        InvitationStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        Guid invitedByUserId)
    {
        Id = id;
        TripId = tripId;
        Email = email.Value;
        NormalizedEmail = email.Normalized;
        Role = role;
        TokenHash = tokenHash;
        Status = status;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        InvitedByUserId = invitedByUserId;
    }

    public Guid Id { get; private set; }

    public Guid TripId { get; private set; }

    /// <summary>The address as the inviter entered it.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>The address in the form Identity stores for matching.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    /// <summary>
    /// The part the recipient will have if they accept. Only
    /// <see cref="TripMemberRole.Editor"/> is issued today; ownership is
    /// granted by promoting an existing member.
    /// </summary>
    public TripMemberRole Role { get; private set; }

    /// <summary>The SHA-256 of the token sent to the recipient.</summary>
    public byte[] TokenHash { get; private set; } = [];

    public InvitationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>The account that sent the invitation, not necessarily its owner.</summary>
    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    /// <summary>When the invitation was declined or revoked.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    /// <summary>
    /// Whether a pending invitation has run out of time. The status is
    /// computed rather than stored so a read never writes.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) =>
        Status == InvitationStatus.Pending && now >= ExpiresAt;

    /// <summary>
    /// The status a reader should see. A pending invitation past its expiry is
    /// expired even though the row has not been updated yet.
    /// </summary>
    public InvitationStatus EffectiveStatus(DateTimeOffset now) =>
        IsExpired(now) ? InvitationStatus.Expired : Status;

    /// <summary>
    /// Creates a pending invitation. The caller supplies the token hash; the
    /// raw token never reaches the domain.
    /// </summary>
    public static Invitation Issue(
        Guid tripId,
        EmailAddress email,
        TripMemberRole role,
        byte[] tokenHash,
        Guid invitedByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (tripId == Guid.Empty)
        {
            throw new ArgumentException("An invitation must name a trip.", nameof(tripId));
        }

        if (string.IsNullOrEmpty(email.Value) || string.IsNullOrEmpty(email.Normalized))
        {
            throw new ArgumentException("An invitation must name an address.", nameof(email));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentException("An invitation must name a role.", nameof(role));
        }

        if (tokenHash is null || tokenHash.Length != TokenHashLength)
        {
            throw new ArgumentException(
                $"An invitation token hash is {TokenHashLength} bytes.",
                nameof(tokenHash));
        }

        if (invitedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "An invitation must name the account that sent it.",
                nameof(invitedByUserId));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException(
                "An invitation must expire after it was created.",
                nameof(expiresAt));
        }

        return new Invitation(
            Guid.NewGuid(),
            tripId,
            email,
            role,
            [.. tokenHash],
            InvitationStatus.Pending,
            createdAt,
            expiresAt,
            invitedByUserId);
    }

    /// <summary>
    /// Records that the recipient accepted. The repository decides that the
    /// recipient is the intended one; the aggregate decides that the
    /// invitation is still usable.
    /// </summary>
    public void Accept(UserId recipient, DateTimeOffset now)
    {
        EnsurePending(now);

        if (recipient.Value == Guid.Empty)
        {
            throw new ArgumentException("An invitation must name an account.", nameof(recipient));
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = now;
        AcceptedByUserId = recipient.Value;
    }

    /// <summary>Records that the recipient declined.</summary>
    public void Decline(UserId recipient, DateTimeOffset now)
    {
        EnsurePending(now);

        if (recipient.Value == Guid.Empty)
        {
            throw new ArgumentException("An invitation must name an account.", nameof(recipient));
        }

        Status = InvitationStatus.Declined;
        DecidedAt = now;
        DecidedByUserId = recipient.Value;
    }

    /// <summary>Records that an owner revoked the invitation.</summary>
    public void Revoke(UserId actor, DateTimeOffset now)
    {
        EnsurePending(now);

        if (actor.Value == Guid.Empty)
        {
            throw new ArgumentException("An invitation must name an account.", nameof(actor));
        }

        Status = InvitationStatus.Revoked;
        DecidedAt = now;
        DecidedByUserId = actor.Value;
    }

    /// <summary>
    /// Records that a pending invitation ran out of time. Unlike acceptance
    /// and decline, this is called at or after the expiry, so it only asks
    /// that the invitation is still pending.
    /// </summary>
    public void Expire(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be changed.");
        }

        Status = InvitationStatus.Expired;
        DecidedAt = now;
    }

    private void EnsurePending(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending invitation can be changed.");
        }

        if (now >= ExpiresAt)
        {
            throw new InvalidOperationException("The invitation has expired.");
        }
    }
}
