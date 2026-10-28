using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tabiji.Infrastructure.Persistence;

namespace Tabiji.Tests;

/// <summary>
/// The migration that gives the database accounts, against a real database.
///
/// Four empty tables beside a table that already holds trips is the kind of
/// change that goes wrong silently, so this is proved on a database that
/// actually holds data: seed at the previous migration, apply the new one, and
/// check every existing row is still there. Then roll back and check the travel
/// rows are still there and the account tables are gone.
/// </summary>
public sealed class AddIdentityMigrationDatabaseTests : IAsyncLifetime, IDisposable
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiIdentityMigrationTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    private const string PreviousMigration = "20261024143525_UnifyStopsAndSavedPlaces";

    private static readonly Guid TripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DestinationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegacyPlaceId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly string[] AccountTables =
    [
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserTokens",
        "AspNetUsers"
    ];

    private TabijiDbContext? _context;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        _context = new TabijiDbContext(options);

        // A previous run that failed may have left the database behind, and
        // the seed below is not idempotent.
        await _context.Database.EnsureDeletedAsync();

        // Stop at the migration immediately before the accounts, so the schema
        // under test is the one that already ships.
        await _context.Database.MigrateAsync(PreviousMigration);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [Trips] ([Id], [Name], [StartsOn], [EndsOn], [Status], [CreatedAt], [Destination])
            VALUES (@tripId, 'Japan', '2027-05-16', '2027-06-05', 'Planning', SYSDATETIMEOFFSET(), NULL);

            INSERT INTO [TripDestinations]
                ([Id], [TripId], [ParentDestinationId], [Name], [SortOrder],
                 [StartsOn], [EndsOn], [Latitude], [Longitude], [Kind], [MigratedFromSavedPlace])
            VALUES (@destinationId, @tripId, NULL, 'Osaka', 0, NULL, NULL, NULL, NULL, 'Destination', 0);

            INSERT INTO [SavedPlaces_Legacy]
                ([Id], [TripId], [DestinationId], [Name], [Latitude], [Longitude],
                 [Category], [Address], [Notes], [PlannedOn], [CreatedAt])
            VALUES (@legacyPlaceId, @tripId, NULL, 'Namba', 34.6660, 135.5000,
                    'Food', 'Namba, Osaka', 'Go early.', '2027-05-18', SYSDATETIMEOFFSET());
            """;

        command.Parameters.AddWithValue("@tripId", TripId);
        command.Parameters.AddWithValue("@destinationId", DestinationId);
        command.Parameters.AddWithValue("@legacyPlaceId", LegacyPlaceId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.Database.EnsureDeletedAsync();
        }

        Dispose();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    [DatabaseFact]
    public async Task Up_AddsTheAccountTablesAndKeepsEveryExistingRow()
    {
        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var table in AccountTables)
        {
            Assert.True(await TableExistsAsync(connection, table), $"{table} was not created.");
        }

        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [Trips] WHERE [Id] = @id", TripId));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [TripDestinations] WHERE [Id] = @id", DestinationId));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [SavedPlaces_Legacy] WHERE [Id] = @id", LegacyPlaceId));

        // The archive is still there: the accounts must not have disturbed the
        // one release of compatibility the previous migration promised.
        Assert.True(await TableExistsAsync(connection, "SavedPlaces_Legacy"));
        Assert.False(await TableExistsAsync(connection, "SavedPlaces"));

        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM [AspNetUsers]"));

        await using var destination = connection.CreateCommand();
        destination.CommandText =
            "SELECT [Name], [Kind] FROM [TripDestinations] WHERE [Id] = @id;";
        destination.Parameters.AddWithValue("@id", DestinationId);

        await using var reader = await destination.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("Osaka", reader.GetString(0));
        Assert.Equal("Destination", reader.GetString(1));
    }

    [DatabaseFact]
    public async Task Down_RemovesTheAccountTablesAndStillKeepsEveryExistingRow()
    {
        await _context!.Database.MigrateAsync();
        await _context.Database.MigrateAsync(PreviousMigration);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var table in AccountTables)
        {
            Assert.False(await TableExistsAsync(connection, table), $"{table} was not dropped.");
        }

        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [Trips] WHERE [Id] = @id", TripId));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [TripDestinations] WHERE [Id] = @id", DestinationId));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [SavedPlaces_Legacy] WHERE [Id] = @id", LegacyPlaceId));
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@name, 'U') IS NULL THEN 0 ELSE 1 END;";
        command.Parameters.AddWithValue("@name", name);

        return (int)(await command.ExecuteScalarAsync())! == 1;
    }

    private static async Task<int> CountAsync(SqlConnection connection, string sql, Guid? id = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (id is not null)
        {
            command.Parameters.AddWithValue("@id", id.Value);
        }

        return (int)(await command.ExecuteScalarAsync())!;
    }
}
