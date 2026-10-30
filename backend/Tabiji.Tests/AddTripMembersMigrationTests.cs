using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The migration that gives every trip an owner, checked without a database.
///
/// The table is new and nothing that already exists is altered, which is what
/// makes it safe beside trips that were created before accounts existed. The
/// one piece of SQL assigns those trips, and only when the account is
/// unambiguous.
/// </summary>
public sealed class AddTripMembersMigrationTests
{
    private static readonly string[] Columns =
    [
        "GrantedAt",
        "Role",
        "TripId",
        "UserId"
    ];

    private static List<MigrationOperation> Operations(string methodName)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var method = typeof(Migration).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        method.Invoke(new AddTripMembers(), [builder]);

        return [.. builder.Operations];
    }

    private static CreateTableOperation Table(IEnumerable<MigrationOperation> operations) =>
        operations.OfType<CreateTableOperation>().Single(operation => operation.Name == "TripMembers");

    [Fact]
    public void Up_CreatesOnlyTheMembershipTable()
    {
        var created = Operations("Up")
            .OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .ToArray();

        Assert.Equal(["TripMembers"], created);
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
    }

    [Fact]
    public void Up_LeavesTheLegacyColumnsAndTheArchiveAlone()
    {
        // Trips.Destination and TripDestinations.MigratedFromSavedPlace are
        // kept for one release, and SavedPlaces_Legacy is an archive. None of
        // them may be named by this migration.
        var sql = Operations("Up").OfType<SqlOperation>().Single().Sql;

        Assert.DoesNotContain("Trips].[Destination]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("MigratedFromSavedPlace", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SavedPlaces_Legacy", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_GivesTheTableTheColumnsTheMembershipNeeds()
    {
        var table = Table(Operations("Up"));

        Assert.Equal(Columns, table.Columns.Select(column => column.Name).Order(StringComparer.Ordinal));

        var tripId = table.Columns.Single(column => column.Name == "TripId");
        var userId = table.Columns.Single(column => column.Name == "UserId");
        var role = table.Columns.Single(column => column.Name == "Role");
        var grantedAt = table.Columns.Single(column => column.Name == "GrantedAt");

        Assert.All([tripId, userId], column =>
        {
            Assert.Equal(typeof(Guid), column.ClrType);
            Assert.Equal("uniqueidentifier", column.ColumnType);
            Assert.False(column.IsNullable);
        });

        Assert.Equal(typeof(string), role.ClrType);
        Assert.Equal(20, role.MaxLength);
        Assert.False(role.IsNullable);

        Assert.Equal(typeof(DateTimeOffset), grantedAt.ClrType);
        Assert.Equal("datetimeoffset", grantedAt.ColumnType);
        Assert.False(grantedAt.IsNullable);
    }

    [Fact]
    public void Up_KeysTheTableOnTheTripAndTheAccount()
    {
        var key = Table(Operations("Up")).PrimaryKey;

        Assert.NotNull(key);
        Assert.Equal(["TripId", "UserId"], key.Columns);
        Assert.Equal("PK_TripMembers", key.Name);
    }

    [Fact]
    public void Up_LinksTheMembershipToItsTripAndItsAccount()
    {
        var foreignKeys = Table(Operations("Up")).ForeignKeys;

        Assert.Equal(2, foreignKeys.Count);

        var toTrip = foreignKeys.Single(key => key.PrincipalTable == "Trips");
        var toAccount = foreignKeys.Single(key => key.PrincipalTable == "AspNetUsers");

        Assert.Equal("TripId", Assert.Single(toTrip.Columns));
        Assert.Equal(ReferentialAction.Cascade, toTrip.OnDelete);

        Assert.Equal("UserId", Assert.Single(toAccount.Columns));
        Assert.Equal(ReferentialAction.NoAction, toAccount.OnDelete);
    }

    [Fact]
    public void Up_IndexesTheAccountSoItsTripsCanBeFound()
    {
        var index = Operations("Up")
            .OfType<CreateIndexOperation>()
            .Single();

        Assert.Equal("TripMembers", index.Table);
        Assert.Equal("UserId", Assert.Single(index.Columns));
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Up_AssignsExistingTripsOnlyWhenThereIsOneAccount()
    {
        var sql = Operations("Up").OfType<SqlOperation>().Single().Sql;

        Assert.Contains("IF (SELECT COUNT(*) FROM [AspNetUsers]) = 1", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO [TripMembers]", sql, StringComparison.Ordinal);

        // An ambiguous database is left unowned rather than guessed at, and
        // the migration never refuses to run: the trips are still there, and
        // verify-migration.sql reports the ones without an owner.
        Assert.DoesNotContain("THROW", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUNCATE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Down_DropsOnlyTheMembershipTable()
    {
        var operations = Operations("Down");

        Assert.Equal(
            ["TripMembers"],
            operations.OfType<DropTableOperation>().Select(operation => operation.Name));

        Assert.Empty(operations.OfType<SqlOperation>());
    }
}
