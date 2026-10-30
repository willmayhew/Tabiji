using Tabiji.Domain.Membership;

namespace Tabiji.Tests;

public sealed class MembershipTests
{
    private static readonly Guid TripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset GrantedAt = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UserId_KeepsTheAccountItWraps()
    {
        var owner = UserId.Create(OwnerId);

        Assert.Equal(OwnerId, owner.Value);
        Assert.Equal(OwnerId.ToString("D"), owner.ToString());
    }

    [Fact]
    public void UserId_RefusesTheEmptyAccount()
    {
        var exception = Assert.Throws<ArgumentException>(() => UserId.Create(Guid.Empty));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void UserId_IsTheSameValueForTheSameAccount()
    {
        // Value semantics matter: a membership is found by comparing accounts,
        // never by comparing references.
        Assert.Equal(UserId.Create(OwnerId), UserId.Create(OwnerId));
        Assert.NotEqual(UserId.Create(OwnerId), UserId.Create(TripId));
    }

    [Fact]
    public void Own_MakesTheAccountTheOwnerOfTheTrip()
    {
        var member = TripMember.Own(TripId, UserId.Create(OwnerId), GrantedAt);

        Assert.Equal(TripId, member.TripId);
        Assert.Equal(OwnerId, member.UserId);
        Assert.Equal(TripMemberRole.Owner, member.Role);
        Assert.Equal(GrantedAt, member.GrantedAt);
    }

    [Fact]
    public void Own_RefusesATripThatIsNotNamed()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TripMember.Own(Guid.Empty, UserId.Create(OwnerId), GrantedAt));

        Assert.Equal("tripId", exception.ParamName);
    }

    [Fact]
    public void Own_RefusesAnAccountThatIsNotNamed()
    {
        // default(UserId) is the one way an empty account can reach the
        // factory: UserId.Create refuses it at the boundary.
        var exception = Assert.Throws<ArgumentException>(
            () => TripMember.Own(TripId, default, GrantedAt));

        Assert.Equal("owner", exception.ParamName);
    }

    [Fact]
    public void Grant_AddsAnEditor()
    {
        var member = TripMember.Grant(
            TripId,
            UserId.Create(OwnerId),
            TripMemberRole.Editor,
            GrantedAt);

        Assert.Equal(TripId, member.TripId);
        Assert.Equal(OwnerId, member.UserId);
        Assert.Equal(TripMemberRole.Editor, member.Role);
        Assert.Equal(GrantedAt, member.GrantedAt);
        Assert.False(member.IsOwner);
    }

    [Fact]
    public void Grant_CanAddACoOwner()
    {
        var member = TripMember.Grant(
            TripId,
            UserId.Create(OwnerId),
            TripMemberRole.Owner,
            GrantedAt);

        Assert.True(member.IsOwner);
    }

    [Fact]
    public void Grant_RefusesATripThatIsNotNamed()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TripMember.Grant(
                Guid.Empty,
                UserId.Create(OwnerId),
                TripMemberRole.Editor,
                GrantedAt));

        Assert.Equal("tripId", exception.ParamName);
    }

    [Fact]
    public void Grant_RefusesAnAccountThatIsNotNamed()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TripMember.Grant(
                TripId,
                default,
                TripMemberRole.Editor,
                GrantedAt));

        Assert.Equal("member", exception.ParamName);
    }

    [Fact]
    public void Grant_RefusesARoleThatDoesNotExist()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TripMember.Grant(
                TripId,
                UserId.Create(OwnerId),
                (TripMemberRole)99,
                GrantedAt));

        Assert.Equal("role", exception.ParamName);
    }

    [Fact]
    public void ChangeRole_MovesAPersonBetweenTheRoles()
    {
        var member = TripMember.Grant(
            TripId,
            UserId.Create(OwnerId),
            TripMemberRole.Editor,
            GrantedAt);

        member.ChangeRole(TripMemberRole.Owner);

        Assert.True(member.IsOwner);

        member.ChangeRole(TripMemberRole.Editor);

        Assert.False(member.IsOwner);
    }

    [Fact]
    public void ChangeRole_RefusesARoleThatDoesNotExist()
    {
        var member = TripMember.Own(TripId, UserId.Create(OwnerId), GrantedAt);

        var exception = Assert.Throws<ArgumentException>(
            () => member.ChangeRole((TripMemberRole)99));

        Assert.Equal("role", exception.ParamName);
        Assert.True(member.IsOwner);
    }
}
