using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The migration that adds invitations, checked without a database.
///
/// The table is new and nothing that already exists is altered, which is what
/// makes it safe beside the trips and memberships that already ship. The token
/// is stored as a hash, and the two unique indexes are what make a token
/// single-use and a pending invitation single.
/// </summary>
public sealed class AddTripInvitationsMigrationTests
{
    private static readonly string[] Columns =
    [
        "AcceptedAt",
        "AcceptedByUserId",
        "CreatedAt",
        "DecidedAt",
        "DecidedByUserId",
        "Email",
        "ExpiresAt",
        "Id",
        "InvitedByUserId",
        "NormalizedEmail",
        "Role",
        "Status",
        "TokenHash",
        "TripId"
    ];

    private static List<MigrationOperation> Operations(string methodName)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var method = typeof(Migration).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        method.Invoke(new AddTripInvitations(), [builder]);

        return [.. builder.Operations];
    }

    private static CreateTableOperation Table(IEnumerable<MigrationOperation> operations) =>
        operations.OfType<CreateTableOperation>().Single(operation => operation.Name == "Invitations");

    [Fact]
    public void Up_CreatesOnlyTheInvitationsTable()
    {
        var created = Operations("Up")
            .OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .ToArray();

        Assert.Equal(["Invitations"], created);
    }

    [Fact]
    public void Up_AltersOrDropsNothingThatAlreadyExists()
    {
        var operations = Operations("Up");

        Assert.Empty(operations.OfType<AddColumnOperation>());
        Assert.Empty(operations.OfType<AlterColumnOperation>());
        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<DropTableOperation>());
        Assert.Empty(operations.OfType<RenameTableOperation>());
        Assert.Empty(operations.OfType<RenameColumnOperation>());
        Assert.Empty(operations.OfType<DropIndexOperation>());
        Assert.Empty(operations.OfType<SqlOperation>());
    }

    [Fact]
    public void Up_GivesTheTableTheColumnsAnInvitationNeeds()
    {
        var table = Table(Operations("Up"));

        Assert.Equal(Columns, table.Columns.Select(column => column.Name).Order(StringComparer.Ordinal));

        var id = table.Columns.Single(column => column.Name == "Id");
        var tripId = table.Columns.Single(column => column.Name == "TripId");
        var invitedBy = table.Columns.Single(column => column.Name == "InvitedByUserId");
        var acceptedBy = table.Columns.Single(column => column.Name == "AcceptedByUserId");
        var decidedBy = table.Columns.Single(column => column.Name == "DecidedByUserId");

        Assert.All([id, tripId, invitedBy], column =>
        {
            Assert.Equal(typeof(Guid), column.ClrType);
            Assert.Equal("uniqueidentifier", column.ColumnType);
            Assert.False(column.IsNullable);
        });

        Assert.All([acceptedBy, decidedBy], column =>
        {
            Assert.Equal(typeof(Guid), column.ClrType);
            Assert.Equal("uniqueidentifier", column.ColumnType);
            Assert.True(column.IsNullable);
        });

        var email = table.Columns.Single(column => column.Name == "Email");
        var normalized = table.Columns.Single(column => column.Name == "NormalizedEmail");

        Assert.All([email, normalized], column =>
        {
            Assert.Equal(typeof(string), column.ClrType);
            Assert.Equal(256, column.MaxLength);
            Assert.Equal("nvarchar(256)", column.ColumnType);
            Assert.False(column.IsNullable);
        });

        var role = table.Columns.Single(column => column.Name == "Role");
        var status = table.Columns.Single(column => column.Name == "Status");

        Assert.All([role, status], column =>
        {
            Assert.Equal(typeof(string), column.ClrType);
            Assert.Equal(20, column.MaxLength);
            Assert.Equal("nvarchar(20)", column.ColumnType);
            Assert.False(column.IsNullable);
        });

        var createdAt = table.Columns.Single(column => column.Name == "CreatedAt");
        var expiresAt = table.Columns.Single(column => column.Name == "ExpiresAt");
        var acceptedAt = table.Columns.Single(column => column.Name == "AcceptedAt");
        var decidedAt = table.Columns.Single(column => column.Name == "DecidedAt");

        Assert.All([createdAt, expiresAt], column =>
        {
            Assert.Equal(typeof(DateTimeOffset), column.ClrType);
            Assert.Equal("datetimeoffset", column.ColumnType);
            Assert.False(column.IsNullable);
        });

        Assert.All([acceptedAt, decidedAt], column =>
        {
            Assert.Equal(typeof(DateTimeOffset), column.ClrType);
            Assert.Equal("datetimeoffset", column.ColumnType);
            Assert.True(column.IsNullable);
        });
    }

    [Fact]
    public void Up_KeepsTheTokenOnlyAsAHash()
    {
        var table = Table(Operations("Up"));

        var hash = table.Columns.Single(column => column.Name == "TokenHash");

        Assert.Equal(typeof(byte[]), hash.ClrType);
        Assert.Equal("varbinary(32)", hash.ColumnType);
        Assert.False(hash.IsNullable);

        // The token itself is a bearer credential; no column may hold it.
        Assert.DoesNotContain(table.Columns, column => column.Name == "Token");
    }

    [Fact]
    public void Up_KeysTheTableOnItsOwnId()
    {
        var key = Table(Operations("Up")).PrimaryKey;

        Assert.NotNull(key);
        Assert.Equal(["Id"], key.Columns);
        Assert.Equal("PK_Invitations", key.Name);
    }

    [Fact]
    public void Up_LinksTheInvitationToItsTripAndItsAccounts()
    {
        var foreignKeys = Table(Operations("Up")).ForeignKeys;

        Assert.Equal(4, foreignKeys.Count);

        var toTrip = foreignKeys.Single(key => key.PrincipalTable == "Trips");

        Assert.Equal("TripId", Assert.Single(toTrip.Columns));
        Assert.Equal(ReferentialAction.Cascade, toTrip.OnDelete);

        var toAccounts = foreignKeys
            .Where(key => key.PrincipalTable == "AspNetUsers")
            .ToArray();

        // Sender, acceptor and decider. Deleting an account must not quietly
        // erase the record of an invitation.
        Assert.Equal(3, toAccounts.Length);
        Assert.All(toAccounts, key => Assert.Equal(ReferentialAction.NoAction, key.OnDelete));
        Assert.Equal(
            ["AcceptedByUserId", "DecidedByUserId", "InvitedByUserId"],
            toAccounts.SelectMany(key => key.Columns).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Up_AllowsOnePendingInvitationPerAddressAndTrip()
    {
        var index = Operations("Up")
            .OfType<CreateIndexOperation>()
            .Single(operation => operation.Name == "IX_Invitations_TripId_NormalizedEmail_Pending");

        Assert.Equal("Invitations", index.Table);
        Assert.Equal(["TripId", "NormalizedEmail"], index.Columns);
        Assert.True(index.IsUnique);
        Assert.Equal("[Status] = 'Pending'", index.Filter);
    }

    [Fact]
    public void Up_ResolvesATokenOnce()
    {
        var index = Operations("Up")
            .OfType<CreateIndexOperation>()
            .Single(operation => operation.Name == "IX_Invitations_TokenHash");

        Assert.Equal("Invitations", index.Table);
        Assert.Equal("TokenHash", Assert.Single(index.Columns));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Up_FindsAnAccountsInvitations()
    {
        var index = Operations("Up")
            .OfType<CreateIndexOperation>()
            .Single(operation => operation.Name == "IX_Invitations_NormalizedEmail_Status");

        Assert.Equal(["NormalizedEmail", "Status"], index.Columns);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Up_StatesTheLifecycleInTheDatabase()
    {
        var constraints = Table(Operations("Up")).CheckConstraints
            .ToDictionary(constraint => constraint.Name, constraint => constraint.Sql);

        Assert.Contains("CK_Invitations_Expiry", constraints.Keys);
        Assert.Contains("[ExpiresAt] > [CreatedAt]", constraints["CK_Invitations_Expiry"]);

        Assert.Contains("CK_Invitations_Status", constraints.Keys);
        Assert.Contains("'Pending'", constraints["CK_Invitations_Status"]);

        Assert.Contains("CK_Invitations_Role", constraints.Keys);
        Assert.Contains("'Editor'", constraints["CK_Invitations_Role"]);
    }

    [Fact]
    public void Down_DropsOnlyTheInvitationsTable()
    {
        var operations = Operations("Down");

        Assert.Equal(
            ["Invitations"],
            operations.OfType<DropTableOperation>().Select(operation => operation.Name));

        Assert.Empty(operations.OfType<SqlOperation>());
    }
}
