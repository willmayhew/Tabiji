using Tabiji.Infrastructure.Security;

namespace Tabiji.Tests;

/// <summary>
/// The token an invitation link carries.
///
/// The point of these tests is that the token is long, URL-safe and random,
/// and that what is stored is a hash rather than the token itself.
/// </summary>
public sealed class InvitationTokenTests
{
    [Fact]
    public void Issue_ReturnsAUrlSafeTokenAndItsSha256()
    {
        var tokens = new InvitationTokens();

        var issued = tokens.Issue();

        Assert.Equal(43, issued.Token.Length);
        Assert.DoesNotContain('=', issued.Token);
        Assert.DoesNotContain('+', issued.Token);
        Assert.DoesNotContain('/', issued.Token);

        Assert.Equal(32, issued.Hash.Length);
        Assert.Equal(issued.Hash, tokens.Hash(issued.Token));
    }

    [Fact]
    public void Issue_ReturnsADifferentTokenEachTime()
    {
        var tokens = new InvitationTokens();

        var first = tokens.Issue();
        var second = tokens.Issue();

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        var tokens = new InvitationTokens();
        var issued = tokens.Issue();

        Assert.Equal(tokens.Hash(issued.Token), tokens.Hash(issued.Token));
    }

    [Fact]
    public void Hash_IsNotTheTokenItself()
    {
        var tokens = new InvitationTokens();
        var issued = tokens.Issue();

        Assert.NotEqual(issued.Token, Convert.ToBase64String(issued.Hash));
        Assert.NotEqual(issued.Token, Convert.ToHexString(issued.Hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Hash_RefusesAnEmptyToken(string token)
    {
        var tokens = new InvitationTokens();

        var exception = Assert.Throws<ArgumentException>(() => tokens.Hash(token));

        Assert.Equal("token", exception.ParamName);
    }
}
