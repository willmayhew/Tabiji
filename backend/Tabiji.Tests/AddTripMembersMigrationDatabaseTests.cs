using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tabiji.Infrastructure.Persistence;

namespace Tabiji.Tests;

/// <summary>
/// The migration that gives every trip an owner, against a real database.
///
/// The point of it is the data, not the schema: trips that predate accounts
/// must come out of the migration still owned by the person who made them,
/// and a database that cannot say who that is must refuse rather than guess.
/// </summary>
public sealed class AddTripMembersMigrationDatabaseTests : IAsyncLifetime, IDisposable
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiTripMembersMigrationTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    private const string PreviousMigration = "20261028220131_AddIdentity";

    private static readonly Guid TripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DestinationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegacyPlaceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OwnerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SecondAccountId = Guid.Parse("55555555-5555-5555-5555-555555555555");

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

        // Stop at the accounts, so the schema under test is the one that ships
        // and the trips have never had an owner.
        await _context.Database.MigrateAsync(PreviousMigration);
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
    public async Task Up_GivesEveryExistingTripToTheOnlyAccountAndKeepsEveryRow()
    {
        await SeedAccountAsync(OwnerId, "owner@example.com");
        await SeedTripAsync();

        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [TripMembers]"));
        Assert.Equal(
            1,
            await CountAsync(
                connection,
                "SELECT COUNT(*) FROM [TripMembers] WHERE [TripId] = @trip AND [UserId] = @owner AND [Role] = 'Owner'",
                ("@trip", TripId),
                ("@owner", OwnerId)));

        // The travel data is exactly as it was: ownership is added beside it,
        // never through it.
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [Trips] WHERE [Id] = @id", ("@id", TripId)));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [TripDestinations] WHERE [Id] = @id", ("@id", DestinationId)));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [SavedPlaces_Legacy] WHERE [Id] = @id", ("@id", LegacyPlaceId)));

        Assert.True(await ColumnExistsAsync(connection, "Trips", "Destination"));
        Assert.True(await ColumnExistsAsync(connection, "TripDestinations", "MigratedFromSavedPlace"));
        Assert.True(await TableExistsAsync(connection, "SavedPlaces_Legacy"));
    }

    [DatabaseFact]
    public async Task Up_LeavesTheTripsUnownedWhenMoreThanOneAccountExists()
    {
        await SeedAccountAsync(OwnerId, "owner@example.com");
        await SeedAccountAsync(SecondAccountId, "friend@example.com");
        await SeedTripAsync();

        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Who owns them is a human decision. The migration applies, the trips
        // stay exactly where they are, and nothing is guessed.
        Assert.True(await TableExistsAsync(connection, "TripMembers"));
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM [TripMembers]"));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [Trips] WHERE [Id] = @id", ("@id", TripId)));
    }

    [DatabaseFact]
    public async Task Up_LeavesTheTripsUnownedWhenThereIsNoAccountYet()
    {
        // Accounts arrive before ownership does, but a database can still hold
        // trips that nobody has claimed. That must not block the upgrade.
        await SeedTripAsync();

        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        Assert.True(await TableExistsAsync(connection, "TripMembers"));
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM [TripMembers]"));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [Trips] WHERE [Id] = @id", ("@id", TripId)));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [SavedPlaces_Legacy] WHERE [Id] = @id", ("@id", LegacyPlaceId)));
    }

    [DatabaseFact]
    public async Task Up_LeavesAnEmptyDatabaseEmpty()
    {
        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        Assert.True(await TableExistsAsync(connection, "TripMembers"));
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM [TripMembers]"));
    }

    [DatabaseFact]
    public async Task Down_DropsTheMembershipAndKeepsEveryTrip()
    {
        await SeedAccountAsync(OwnerId, "owner@example.com");
        await SeedTripAsync();

        await _context!.Database.MigrateAsync();
        await _context.Database.MigrateAsync(PreviousMigration);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        Assert.False(await TableExistsAsync(connection, "TripMembers"));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [Trips] WHERE [Id] = @id", ("@id", TripId)));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [TripDestinations] WHERE [Id] = @id", ("@id", DestinationId)));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [SavedPlaces_Legacy] WHERE [Id] = @id", ("@id", LegacyPlaceId)));
    }

    private static async Task SeedAccountAsync(Guid userId, string email)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [AspNetUsers]
                ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail],
                 [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp],
                 [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled],
                 [LockoutEnd], [LockoutEnabled], [AccessFailedCount])
            VALUES
                (@id, @email, UPPER(@email), @email, UPPER(@email),
                 1, 'hash', 'stamp', 'stamp', NULL, 0, 0, NULL, 1, 0);
            """;

        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@email", email);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedTripAsync()
    {
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

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@name, 'U') IS NULL THEN 0 ELSE 1 END;";
        command.Parameters.AddWithValue("@name", name);

        return (int)(await command.ExecuteScalarAsync())! == 1;
    }

    private static async Task<bool> ColumnExistsAsync(
        SqlConnection connection,
        string table,
        string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(@table) AND name = @column
            ) THEN 1 ELSE 0 END;
            """;

        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@column", column);

        return (int)(await command.ExecuteScalarAsync())! == 1;
    }

    private static async Task<int> CountAsync(
        SqlConnection connection,
        string sql,
        params (string Name, Guid Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (int)(await command.ExecuteScalarAsync())!;
    }
}
