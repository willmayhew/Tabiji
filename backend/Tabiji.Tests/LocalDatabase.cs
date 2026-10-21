using Microsoft.Data.SqlClient;

namespace Tabiji.Tests;

/// <summary>
/// Whether SQL Server LocalDB can be reached.
///
/// The suite is written to run without a database. A test that has to have
/// one asks this first, and is skipped rather than failed when there is no
/// database to be had.
/// </summary>
internal static class LocalDatabase
{
    /// <summary>
    /// The server as the development setup has it. The database is `master`
    /// because it is always there: the question being asked is whether the
    /// server answers, not whether a particular database exists yet.
    /// </summary>
    private const string ProbeConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    public static bool IsAvailable()
    {
        try
        {
            using var connection = new SqlConnection(ProbeConnectionString);
            connection.Open();

            return true;
        }
        catch (SqlException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
