using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The migration that gives the database accounts, checked without a database.
///
/// The change is additive in the strict sense: it creates four empty tables
/// and indexes them. It alters nothing, drops nothing and moves no row, which
/// is what makes it safe to apply to a database that already holds trips.
/// </summary>
public sealed class AddIdentityMigrationTests
{
    private static readonly string[] AccountTables =
    [
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserTokens",
        "AspNetUsers"
    ];

    private static readonly string[] TravelTables =
    [
        "SavedPlaces_Legacy",
        "TripDestinations",
        "Trips"
    ];

    private static List<MigrationOperation> Operations(string methodName)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var method = typeof(Migration).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        method.Invoke(new AddIdentity(), [builder]);

        return [.. builder.Operations];
    }

    [Fact]
    public void Up_CreatesTheFourAccountTables()
    {
        var created = Operations("Up")
            .OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([.. AccountTables.Order(StringComparer.Ordinal)], created);
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

        // No hand-written SQL: nothing reads or writes a stored row.
        Assert.Empty(operations.OfType<SqlOperation>());
    }

    [Fact]
    public void Up_LeavesTheTravelTablesOutOfIt()
    {
        var touched = Operations("Up")
            .OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .ToArray();

        Assert.All(TravelTables, table => Assert.DoesNotContain(table, touched));
    }

    [Fact]
    public void Up_IndexesOnlyTheAccountTables()
    {
        var indexes = Operations("Up").OfType<CreateIndexOperation>().ToArray();

        Assert.NotEmpty(indexes);
        Assert.All(indexes, index => Assert.StartsWith("AspNet", index.Table, StringComparison.Ordinal));
    }

    [Fact]
    public void Up_LinksTheAccountTablesOnlyToEachOther()
    {
        var foreignKeys = Operations("Up")
            .OfType<CreateTableOperation>()
            .SelectMany(table => table.ForeignKeys)
            .ToArray();

        Assert.NotEmpty(foreignKeys);
        Assert.All(
            foreignKeys,
            key => Assert.StartsWith("AspNet", key.PrincipalTable, StringComparison.Ordinal));
    }

    [Fact]
    public void Up_KeysTheUsersOnAGuid()
    {
        var users = Operations("Up")
            .OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "AspNetUsers");

        var id = users.Columns.Single(column => column.Name == "Id");

        Assert.Equal(typeof(Guid), id.ClrType);
        Assert.Equal("uniqueidentifier", id.ColumnType);
        Assert.False(id.IsNullable);
    }

    [Fact]
    public void Down_DropsOnlyTheAccountTables()
    {
        var dropped = Operations("Down")
            .OfType<DropTableOperation>()
            .Select(operation => operation.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([.. AccountTables.Order(StringComparer.Ordinal)], dropped);
        Assert.Empty(Operations("Down").OfType<SqlOperation>());
    }
}
