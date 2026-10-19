using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Tabiji.Tests;

public sealed class TabijiWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string LocalDbConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=Tabiji;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // The host needs a syntactically valid connection string to build the DbContext
        // options. No connection is opened, so the tests run without a live database.
        builder.UseSetting("ConnectionStrings:Tabiji", LocalDbConnectionString);
    }
}
