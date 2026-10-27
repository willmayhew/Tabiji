using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tabiji.Infrastructure.Persistence;

namespace Tabiji.Tests;

/// <summary>
/// The migration that copies saved places into the unified place table,
/// against a real database.
///
/// The copy is the one part of the change that cannot be checked without a
/// database. The test starts from the schema immediately before the migration,
/// seeds a trip, a destination and a saved place, applies the migration and
/// checks the preserved row, then rolls it back and checks the saved place is
/// restored.
/// </summary>
public sealed class UnifyStopsAndSavedPlacesMigrationTests : IAsyncLifetime, IDisposable
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiUnifyMigrationTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    private const string PreviousMigration = "20261024124232_AddDestinationHierarchy";

    private static readonly Guid TripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ParentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PlaceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UnfiledPlaceId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private TabijiDbContext? _context;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        _context = new TabijiDbContext(options);

        // Stop at the migration immediately before the unified one, so the
        // old SavedPlaces table exists and can be seeded.
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
                 [StartsOn], [EndsOn], [Latitude], [Longitude])
            VALUES (@parentId, @tripId, NULL, 'Osaka', 0, NULL, NULL, NULL, NULL);

            INSERT INTO [SavedPlaces]
                ([Id], [TripId], [DestinationId], [Name], [Latitude], [Longitude],
                 [Category], [Address], [Notes], [PlannedOn], [CreatedAt])
            VALUES (@placeId, @tripId, @parentId, 'Namba', 34.6660, 135.5000,
                    'Food', 'Namba, Osaka', 'Go early.', '2027-05-18', SYSDATETIMEOFFSET());

            INSERT INTO [SavedPlaces]
                ([Id], [TripId], [DestinationId], [Name], [Latitude], [Longitude],
                 [Category], [Address], [Notes], [PlannedOn], [CreatedAt])
            VALUES (@unfiledPlaceId, @tripId, NULL, 'Someday', 35.0000, 135.0000,
                    'Sight', 'Somewhere', 'One day.', '2027-05-19', SYSDATETIMEOFFSET());
            """;

        command.Parameters.AddWithValue("@tripId", TripId);
        command.Parameters.AddWithValue("@parentId", ParentId);
        command.Parameters.AddWithValue("@placeId", PlaceId);
        command.Parameters.AddWithValue("@unfiledPlaceId", UnfiledPlaceId);

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
    public async Task Up_CopiesTheSavedPlaceIntoTheUnifiedTableAndPreservesIt()
    {
        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT [Kind], [ParentDestinationId], [Name], [Latitude], [Longitude],
                   [Category], [Address], [Notes], [PlannedOn], [CreatedAt],
                   [MigratedFromSavedPlace]
            FROM [TripDestinations]
            WHERE [Id] = @placeId;
            """;
        command.Parameters.AddWithValue("@placeId", PlaceId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("Stop", reader.GetString(0));
        Assert.Equal(ParentId, reader.GetGuid(1));
        Assert.Equal("Namba", reader.GetString(2));
        Assert.Equal(34.6660, reader.GetDouble(3));
        Assert.Equal(135.5000, reader.GetDouble(4));
        Assert.Equal("Food", reader.GetString(5));
        Assert.Equal("Namba, Osaka", reader.GetString(6));
        Assert.Equal("Go early.", reader.GetString(7));
        Assert.Equal(new DateOnly(2027, 5, 18), reader.GetFieldValue<DateOnly>(8));
        Assert.True(reader.GetBoolean(10));

        await reader.DisposeAsync();

        Assert.True(await TableExistsAsync(connection, "SavedPlaces_Legacy"));
        Assert.False(await TableExistsAsync(connection, "SavedPlaces"));

        await using var unfiled = connection.CreateCommand();
        unfiled.CommandText =
            """
            SELECT [Kind], [ParentDestinationId], [Name]
            FROM [TripDestinations]
            WHERE [Id] = @unfiledPlaceId;
            """;
        unfiled.Parameters.AddWithValue("@unfiledPlaceId", UnfiledPlaceId);

        await using var unfiledReader = await unfiled.ExecuteReaderAsync();

        Assert.True(await unfiledReader.ReadAsync());
        Assert.Equal("Place", unfiledReader.GetString(0));
        Assert.True(unfiledReader.IsDBNull(1));
        Assert.Equal("Someday", unfiledReader.GetString(2));
    }

    [DatabaseFact]
    public async Task Down_RestoresTheSavedPlaceAndLeavesTheOriginalDestination()
    {
        await _context!.Database.MigrateAsync();
        await _context.Database.MigrateAsync(PreviousMigration);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using (var place = connection.CreateCommand())
        {
            place.CommandText =
                """
                SELECT [Name], [Latitude], [Longitude], [Category], [Address], [Notes], [PlannedOn]
                FROM [SavedPlaces]
                WHERE [Id] = @placeId;
                """;
            place.Parameters.AddWithValue("@placeId", PlaceId);

            await using var reader = await place.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("Namba", reader.GetString(0));
            Assert.Equal(34.6660, reader.GetDouble(1));
            Assert.Equal(135.5000, reader.GetDouble(2));
            Assert.Equal("Food", reader.GetString(3));
            Assert.Equal("Namba, Osaka", reader.GetString(4));
            Assert.Equal("Go early.", reader.GetString(5));
            Assert.Equal(new DateOnly(2027, 5, 18), reader.GetFieldValue<DateOnly>(6));
        }

        await using (var destination = connection.CreateCommand())
        {
            destination.CommandText =
                """
                SELECT [Name]
                FROM [TripDestinations]
                WHERE [Id] = @parentId;
                """;
            destination.Parameters.AddWithValue("@parentId", ParentId);

            var name = await destination.ExecuteScalarAsync();

            Assert.Equal("Osaka", name);
        }

        await using (var migrated = connection.CreateCommand())
        {
            migrated.CommandText =
                "SELECT COUNT(*) FROM [TripDestinations] WHERE [Id] = @placeId;";
            migrated.Parameters.AddWithValue("@placeId", PlaceId);

            Assert.Equal(0, (int)(await migrated.ExecuteScalarAsync())!);
        }

        Assert.False(await TableExistsAsync(connection, "SavedPlaces_Legacy"));
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@name, 'U') IS NULL THEN 0 ELSE 1 END;";
        command.Parameters.AddWithValue("@name", name);

        return (int)(await command.ExecuteScalarAsync())! == 1;
    }
}
