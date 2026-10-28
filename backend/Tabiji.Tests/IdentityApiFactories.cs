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
