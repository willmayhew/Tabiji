namespace Tabiji.Tests;

/// <summary>
/// The host refuses to start when it is told to send invitation emails but not
/// where the links should point: such a deployment would mail localhost, which
/// nobody could open. The setting that supplies the address is checked here
/// rather than discovered later, in a mailbox.
/// </summary>
public sealed class EmailDeliveryConfigurationTests
{
    [Fact]
    public void EmailDeliveryWithoutAPublicAddress_IsRefusedAtStartup()
    {
        using var factory = new EmailDeliveryApiFactory(publicBaseUrl: null);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(
            "Invitations:PublicBaseUrl",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void EmailDeliveryWithAPublicAddress_Starts()
    {
        using var factory = new EmailDeliveryApiFactory("https://tabiji.example");

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }
}
