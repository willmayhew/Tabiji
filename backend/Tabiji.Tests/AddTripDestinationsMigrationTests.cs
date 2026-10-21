using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The data migration, checked without a database.
///
/// A migration that moves data is normally only exercised against a real
/// server. Its operations can also be inspected directly, which keeps the
/// suite database-free while still guarding the backfill.
/// </summary>
public sealed class AddTripDestinationsMigrationTests
{
    private static List<MigrationOperation> UpOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Migration).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(up);
        up.Invoke(new AddTripDestinations(), [builder]);

        return [.. builder.Operations];
    }

    private static List<MigrationOperation> DownOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var down = typeof(Migration).GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(down);
        down.Invoke(new AddTripDestinations(), [builder]);

        return [.. builder.Operations];
    }

    [Fact]
    public void Up_CreatesTheRouteTable()
    {
        var operation = UpOperations().OfType<CreateTableOperation>()
            .SingleOrDefault(candidate => candidate.Name == "TripDestinations");

        Assert.NotNull(operation);
        Assert.Equal(
            ["Id", "TripId", "Name", "SortOrder"],
            operation.Columns.Select(column => column.Name));
    }

    [Fact]
    public void Up_CreatesTheUniqueIndexOnThePosition()
    {
        var operation = UpOperations().OfType<CreateIndexOperation>()
            .SingleOrDefault(candidate => candidate.Name == "IX_TripDestinations_TripId_SortOrder");

        Assert.NotNull(operation);
        Assert.True(operation.IsUnique);
        Assert.Equal(["TripId", "SortOrder"], operation.Columns);
    }

    [Fact]
    public void Up_MakesTheLegacyColumnNullableRatherThanDroppingIt()
    {
        var operation = UpOperations().OfType<AlterColumnOperation>()
            .SingleOrDefault(candidate => candidate.Name == "Destination" && candidate.Table == "Trips");

        Assert.NotNull(operation);
        Assert.True(operation.IsNullable);
    }

    [Fact]
    public void Up_BackfillsEveryLegacyDestinationAsTheFirstStop()
    {
        var operation = UpOperations().OfType<SqlOperation>()
            .SingleOrDefault(candidate => candidate.Sql.Contains("INSERT INTO [TripDestinations]"));

        Assert.NotNull(operation);
        Assert.Contains("LTRIM(RTRIM([Destination]))", operation.Sql);
        Assert.Contains("[SortOrder])", operation.Sql);
        Assert.Contains("<> ''", operation.Sql);
    }

    [Fact]
    public void Up_BackfillsAfterTheTableExists()
    {
        var operations = UpOperations();

        var createIndex = operations.FindIndex(operation =>
            operation is CreateIndexOperation { Name: "IX_TripDestinations_TripId_SortOrder" });
        var backfill = operations.FindIndex(operation =>
            operation is SqlOperation { Sql: var sql } && sql.Contains("INSERT INTO [TripDestinations]"));

        Assert.True(createIndex >= 0);
        Assert.True(backfill > createIndex);
    }

    [Fact]
    public void Down_RestoresTheLegacyColumnBeforeTheRouteIsDropped()
    {
        var operations = DownOperations();

        var restore = operations.FindIndex(operation =>
            operation is SqlOperation { Sql: var sql } && sql.Contains("UPDATE [t]"));
        var drop = operations.FindIndex(operation => operation is DropTableOperation);

        Assert.True(restore >= 0);
        Assert.True(drop > restore);
    }

    [Fact]
    public void Down_RestoresTheFirstStopAndFallsBackForATripWithNoStops()
    {
        var operation = DownOperations().OfType<SqlOperation>()
            .SingleOrDefault(candidate => candidate.Sql.Contains("UPDATE [t]"));

        Assert.NotNull(operation);
        Assert.Contains("ORDER BY [d].[SortOrder]", operation.Sql);
        Assert.Contains("ISNULL", operation.Sql);
    }

    [Fact]
    public void Down_MakesTheLegacyColumnNonNullableAgain()
    {
        var operation = DownOperations().OfType<AlterColumnOperation>()
            .SingleOrDefault(candidate => candidate.Name == "Destination" && candidate.Table == "Trips");

        Assert.NotNull(operation);
        Assert.False(operation.IsNullable);
    }
}
