using Xunit;

namespace Tabiji.Tests;

/// <summary>
/// A fact that needs a database.
///
/// The suite runs without one, so a test that has to have a database is
/// marked with this instead. When SQL Server LocalDB cannot be reached the
/// test is skipped rather than failed, so the suite still runs on a machine
/// that has no database.
/// </summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (!LocalDatabase.IsAvailable())
        {
            Skip = "SQL Server LocalDB is not available.";
        }
    }
}
