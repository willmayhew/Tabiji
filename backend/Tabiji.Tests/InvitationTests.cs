using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Tests;

/// <summary>
/// The invitation aggregate and the address it names.
///
/// The rules here are the ones a single row can decide: the address shape, the
/// lifecycle, and the fact that only a hash is held. Whether the recipient is
/// the intended one, and whether the trip still has an owner, are decided where
/// the rest of the data is visible.
/// </summary>
public sealed class InvitationTests
{
    private static readonly Guid TripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid InvitedById = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RecipientId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = CreatedAt.AddDays(7);

    private static readonly byte[] TokenHash =
        [.. Enumerable.Range(0, Invitation.TokenHashLength).Select(value => (byte)value)];

    private static EmailAddress Address => EmailAddress.Create("  Friend@Example.COM  ");

    private static Invitation Pending(DateTimeOffset? expiresAt = null) =>
        Invitation.Issue(
            TripId,
            Address,
            TripMemberRole.Editor,
            TokenHash,
            InvitedById,
            CreatedAt,
            expiresAt ?? ExpiresAt);

    [Fact]
    public void EmailAddress_KeepsTheAddressAndItsNormalisedForm()
    {
        var address = Address;

        Assert.Equal("Friend@Example.COM", address.Value);
        Assert.Equal("FRIEND@EXAMPLE.COM", address.Normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("friend")]
    [InlineData("@example.com")]
    [InlineData("friend@")]
    [InlineData("friend@@example.com")]
    [InlineData("two words@example.com")]
    public void EmailAddress_RefusesAnAddressThatIsNotOne(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => EmailAddress.Create(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void EmailAddress_RefusesAnAddressLongerThanTheColumn()
    {
        var value = new string('a', EmailAddress.MaxLength) + "@example.com";

        var exception = Assert.Throws<ArgumentException>(() => EmailAddress.Create(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Issue_CreatesAPendingInvitationAndKeepsOnlyTheHash()
    {
        var invitation = Pending();

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal(TripId, invitation.TripId);
        Assert.Equal("Friend@Example.COM", invitation.Email);
        Assert.Equal("FRIEND@EXAMPLE.COM", invitation.NormalizedEmail);
        Assert.Equal(TripMemberRole.Editor, invitation.Role);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Equal(CreatedAt, invitation.CreatedAt);
        Assert.Equal(ExpiresAt, invitation.ExpiresAt);
        Assert.Equal(InvitedById, invitation.InvitedByUserId);
        Assert.Equal(TokenHash, invitation.TokenHash);
        Assert.Null(invitation.AcceptedAt);
        Assert.Null(invitation.AcceptedByUserId);
        Assert.Null(invitation.DecidedAt);
    }

    [Fact]
    public void Issue_DoesNotShareTheCallersHashArray()
    {
        byte[] hash = [.. TokenHash];
        var invitation = Invitation.Issue(
            TripId,
            Address,
            TripMemberRole.Editor,
            hash,
            InvitedById,
            CreatedAt,
            ExpiresAt);

        hash[0] = 255;

        Assert.Equal(TokenHash, invitation.TokenHash);
    }

    [Fact]
    public void Issue_RefusesATripThatIsNotNamed()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Invitation.Issue(Guid.Empty, Address, TripMemberRole.Editor, TokenHash, InvitedById, CreatedAt, ExpiresAt));

        Assert.Equal("tripId", exception.ParamName);
    }

    [Fact]
    public void Issue_RefusesAnAddressThatIsNotNamed()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Invitation.Issue(TripId, default, TripMemberRole.Editor, TokenHash, InvitedById, CreatedAt, ExpiresAt));

        Assert.Equal("email", exception.ParamName);
    }

    [Fact]
    public void Issue_RefusesARoleThatDoesNotExist()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Invitation.Issue(TripId, Address, (TripMemberRole)99, TokenHash, InvitedById, CreatedAt, ExpiresAt));

        Assert.Equal("role", exception.ParamName);
    }

    [Fact]
    public void Issue_RefusesAHashThatIsNotSha256Sized()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Invitation.Issue(TripId, Address, TripMemberRole.Editor, [1, 2, 3], InvitedById, CreatedAt, ExpiresAt));

        Assert.Equal("tokenHash", exception.ParamName);
    }

    [Fact]
    public void Issue_RefusesAnInvitationThatDidNotComeFromAnAccount()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Invitation.Issue(TripId, Address, TripMemberRole.Editor, TokenHash, Guid.Empty, CreatedAt, ExpiresAt));

        Assert.Equal("invitedByUserId", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Issue_RefusesAnInvitationThatExpiresBeforeItStarts(int days)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Invitation.Issue(
                TripId,
                Address,
                TripMemberRole.Editor,
                TokenHash,
                InvitedById,
                CreatedAt,
                CreatedAt.AddDays(days)));

        Assert.Equal("expiresAt", exception.ParamName);
    }

    [Fact]
    public void Accept_RecordsTheRecipientAndTheMoment()
    {
        var invitation = Pending();
        var acceptedAt = CreatedAt.AddDays(1);

        invitation.Accept(UserId.Create(RecipientId), acceptedAt);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(acceptedAt, invitation.AcceptedAt);
        Assert.Equal(RecipientId, invitation.AcceptedByUserId);
        Assert.Equal(InvitationStatus.Accepted, invitation.EffectiveStatus(acceptedAt));
    }

    [Fact]
    public void Accept_RefusesAnInvitationThatHasAlreadyBeenDecided()
    {
        var invitation = Pending();
        invitation.Accept(UserId.Create(RecipientId), CreatedAt);

        Assert.Throws<InvalidOperationException>(
            () => invitation.Accept(UserId.Create(RecipientId), CreatedAt.AddDays(1)));
    }

    [Fact]
    public void Accept_RefusesAnExpiredInvitation()
    {
        var invitation = Pending();

        Assert.Throws<InvalidOperationException>(
            () => invitation.Accept(UserId.Create(RecipientId), ExpiresAt));
    }

    [Fact]
    public void Accept_RefusesAnAccountThatIsNotNamed()
    {
        var invitation = Pending();

        var exception = Assert.Throws<ArgumentException>(
            () => invitation.Accept(default, CreatedAt));

        Assert.Equal("recipient", exception.ParamName);
    }

    [Fact]
    public void Decline_RecordsTheRecipientAndTheMoment()
    {
        var invitation = Pending();
        var decidedAt = CreatedAt.AddHours(2);

        invitation.Decline(UserId.Create(RecipientId), decidedAt);

        Assert.Equal(InvitationStatus.Declined, invitation.Status);
        Assert.Equal(decidedAt, invitation.DecidedAt);
        Assert.Equal(RecipientId, invitation.DecidedByUserId);
        Assert.Null(invitation.AcceptedAt);
    }

    [Fact]
    public void Revoke_RecordsTheOwnerAndTheMoment()
    {
        var invitation = Pending();
        var revokedAt = CreatedAt.AddHours(3);

        invitation.Revoke(UserId.Create(InvitedById), revokedAt);

        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
        Assert.Equal(revokedAt, invitation.DecidedAt);
        Assert.Equal(InvitedById, invitation.DecidedByUserId);
    }

    [Fact]
    public void Expire_RecordsTheEndOfAPendingInvitation()
    {
        var invitation = Pending();

        invitation.Expire(ExpiresAt);

        Assert.Equal(InvitationStatus.Expired, invitation.Status);
        Assert.Equal(ExpiresAt, invitation.DecidedAt);
        Assert.Null(invitation.AcceptedAt);
    }

    [Fact]
    public void Expire_RefusesAnInvitationThatWasAlreadyDecided()
    {
        var invitation = Pending();
        invitation.Decline(UserId.Create(RecipientId), CreatedAt);

        Assert.Throws<InvalidOperationException>(() => invitation.Expire(ExpiresAt));
    }

    [Fact]
    public void IsExpired_IsFalseBeforeTheExpiryAndTrueAtIt()
    {
        var invitation = Pending();

        Assert.False(invitation.IsExpired(ExpiresAt.AddTicks(-1)));
        Assert.True(invitation.IsExpired(ExpiresAt));
        Assert.Equal(InvitationStatus.Pending, invitation.EffectiveStatus(ExpiresAt.AddTicks(-1)));
        Assert.Equal(InvitationStatus.Expired, invitation.EffectiveStatus(ExpiresAt));
    }
}
