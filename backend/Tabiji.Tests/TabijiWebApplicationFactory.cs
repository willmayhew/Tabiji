using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Tabiji.Tests;

public class TabijiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected const string LocalDbConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=Tabiji;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // The host needs a syntactically valid connection string to build the DbContext
        // options. No connection is opened, so the tests run without a live database.
        builder.UseSetting("ConnectionStrings:Tabiji", LocalDbConnectionString);

        ConfigureServices(builder);
    }

    /// <summary>
    /// Hook for a test that needs to replace an infrastructure service, for example to swap
    /// the trip repository for an in-memory fake. The default leaves the real services alone.
    /// </summary>
    protected virtual void ConfigureServices(IWebHostBuilder builder)
    {
    }
}
