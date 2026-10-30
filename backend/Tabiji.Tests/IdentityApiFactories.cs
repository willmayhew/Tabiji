using Microsoft.AspNetCore.Hosting;

namespace Tabiji.Tests;

/// <summary>
/// The host, pointed at a database of its own.
///
/// The account tests create real users through real Identity, so they need a
/// real store. They get one of their own rather than the development database,
/// which is left exactly as the developer had it.
/// </summary>
internal sealed class AuthApiFactory : TabijiWebApplicationFactory
{
    internal const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiAuthApiTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("ConnectionStrings:Tabiji", ConnectionString);
    }
}

/// <summary>
/// The same host, with a database of its own, for the tests that prove two real
/// accounts cannot reach each other's trips. A separate database from the
/// account tests, because the two classes run side by side and would otherwise
/// delete each other's store.
/// </summary>
internal sealed class OwnershipApiFactory : TabijiWebApplicationFactory
{
    internal const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiOwnershipApiTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("ConnectionStrings:Tabiji", ConnectionString);
    }
}

/// <summary>
/// The same host, declared to be a different environment, so the settings that
/// differ between development and production can be read without guessing.
/// </summary>
internal sealed class EnvironmentApiFactory(string environmentName) : TabijiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseEnvironment(environmentName);
    }
}
