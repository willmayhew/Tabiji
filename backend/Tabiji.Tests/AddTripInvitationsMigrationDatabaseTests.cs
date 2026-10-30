using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tabiji.Infrastructure.Persistence;

namespace Tabiji.Tests;

/// <summary>
/// The migration that adds invitations, against a real database.
///
/// The point is the constraints, not only the columns: one pending invitation
/// per address and trip, one row per token hash, and a lifecycle the database
/// itself refuses to leave. The trips and memberships that already exist are
/// untouched.
/// </summary>
public sealed class AddTripInvitationsMigrationDatabaseTests : IAsyncLifetime, IDisposable
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TabijiInvitationMigrationTests;" +
        "Trusted_Connection=True;TrustServerCertificate=True";

    private const string PreviousMigration = "20261029190135_AddTripMembers";

    private static readonly Guid TripId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SecondAccountId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid InvitationId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SecondInvitationId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static readonly byte[] TokenHash =
        [.. Enumerable.Range(0, 32).Select(value => (byte)value)];

    private static readonly byte[] OtherTokenHash =
        [.. Enumerable.Range(32, 32).Select(value => (byte)value)];

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static readonly int[] DuplicateKeyNumbers = [2601, 2627];

    private TabijiDbContext? _context;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        _context = new TabijiDbContext(options);

        await _context.Database.EnsureDeletedAsync();
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

    [Fact]
    public async Task Up_CreatesTheInvitationsTableAndKeepsTripsAndMembers()
    {
        await SeedAsync();

        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        Assert.True(await TableExistsAsync(connection, "Invitations"));
        Assert.True(await ColumnExistsAsync(connection, "Invitations", "TokenHash"));
        Assert.True(await ColumnExistsAsync(connection, "Invitations", "NormalizedEmail"));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [Trips]"));
        Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM [TripMembers]"));
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM [Invitations]"));
    }

    [Fact]
    public async Task Up_RefusesASecondPendingInvitationForTheSameAddress()
    {
        await SeedAsync();
        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await InsertInvitationAsync(connection, InvitationId, "friend@example.com", TokenHash);

        var exception = await Assert.ThrowsAsync<SqlException>(
            () => InsertInvitationAsync(
                connection,
                SecondInvitationId,
                "friend@example.com",
                OtherTokenHash));

        Assert.Contains(exception.Number, DuplicateKeyNumbers);

        // A decided invitation no longer blocks a fresh one.
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "UPDATE [Invitations] SET [Status] = 'Revoked' WHERE [Id] = @id;";
            command.Parameters.AddWithValue("@id", InvitationId);
            await command.ExecuteNonQueryAsync();
        }

        await InsertInvitationAsync(connection, SecondInvitationId, "friend@example.com", OtherTokenHash);

        Assert.Equal(2, await CountAsync(connection, "SELECT COUNT(*) FROM [Invitations]"));
    }

    [Fact]
    public async Task Up_ResolvesATokenHashOnce()
    {
        await SeedAsync();
        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        await InsertInvitationAsync(connection, InvitationId, "friend@example.com", TokenHash);

        var exception = await Assert.ThrowsAsync<SqlException>(
            () => InsertInvitationAsync(
                connection,
                SecondInvitationId,
                "other@example.com",
                TokenHash));

        Assert.Contains(exception.Number, DuplicateKeyNumbers);
    }

    [Fact]
    public async Task Up_RefusesAnInvitationThatExpiresBeforeItIsCreated()
    {
        await SeedAsync();
        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(
            () => InsertInvitationAsync(
                connection,
                InvitationId,
                "friend@example.com",
                TokenHash,
                createdAt: CreatedAt,
                expiresAt: CreatedAt));

        Assert.Equal(547, exception.Number);
    }

    [Theory]
    [InlineData("Bogus", "Editor")]
    [InlineData("Pending", "Bogus")]
    public async Task Up_RefusesAStatusOrRoleOutsideTheVocabulary(string status, string role)
    {
        await SeedAsync();
        await _context!.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(
            () => InsertInvitationAsync(
                connection,
                InvitationId,
                "friend@example.com",
                TokenHash,
                status: status,
                role: role));

        Assert.Equal(547, exception.Number);
    }

    [Fact]
    public async Task Down_DropsOnlyTheInvitationsTableAndKeepsTrips()
    {
        await SeedAsync();
        await _context!.Database.MigrateAsync();

        await using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await InsertInvitationAsync(connection, InvitationId, "friend@example.com", TokenHash);
        }

        await _context.Database.MigrateAsync(PreviousMigration);

        await using var after = new SqlConnection(ConnectionString);
        await after.OpenAsync();

        Assert.False(await TableExistsAsync(after, "Invitations"));
        Assert.Equal(1, await CountAsync(after, "SELECT COUNT(*) FROM [Trips]"));
        Assert.Equal(1, await CountAsync(after, "SELECT COUNT(*) FROM [TripMembers]"));
    }

    private static async Task SeedAsync()
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
                (@owner, 'owner@example.com', 'OWNER@EXAMPLE.COM',
                 'owner@example.com', 'OWNER@EXAMPLE.COM',
                 1, 'hash', 'stamp', 'stamp', NULL, 0, 0, NULL, 1, 0),
                (@second, 'second@example.com', 'SECOND@EXAMPLE.COM',
                 'second@example.com', 'SECOND@EXAMPLE.COM',
                 1, 'hash', 'stamp', 'stamp', NULL, 0, 0, NULL, 1, 0);

            INSERT INTO [Trips] ([Id], [Name], [StartsOn], [EndsOn], [Status], [CreatedAt], [Destination])
            VALUES (@trip, 'Japan', '2027-05-16', '2027-06-05', 'Planning', SYSDATETIMEOFFSET(), NULL);

            INSERT INTO [TripMembers] ([TripId], [UserId], [Role], [GrantedAt])
            VALUES (@trip, @owner, 'Owner', SYSDATETIMEOFFSET());
            """;

        command.Parameters.AddWithValue("@owner", OwnerId);
        command.Parameters.AddWithValue("@second", SecondAccountId);
        command.Parameters.AddWithValue("@trip", TripId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertInvitationAsync(
        SqlConnection connection,
        Guid id,
        string email,
        byte[] hash,
        string status = "Pending",
        string role = "Editor",
        DateTimeOffset? createdAt = null,
        DateTimeOffset? expiresAt = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO [Invitations]
                ([Id], [TripId], [Email], [NormalizedEmail], [Role], [TokenHash], [Status],
                 [CreatedAt], [ExpiresAt], [InvitedByUserId])
            VALUES
                (@id, @trip, @email, UPPER(@email), @role, @hash, @status,
                 @createdAt, @expiresAt, @invitedBy);
            """;

        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@trip", TripId);
        command.Parameters.AddWithValue("@email", email);
        command.Parameters.AddWithValue("@role", role);
        command.Parameters.AddWithValue("@hash", hash);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@createdAt", createdAt ?? CreatedAt);
        command.Parameters.AddWithValue("@expiresAt", expiresAt ?? CreatedAt.AddDays(7));
        command.Parameters.AddWithValue("@invitedBy", OwnerId);

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

    private static async Task<int> CountAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (int)(await command.ExecuteScalarAsync())!;
    }
}
