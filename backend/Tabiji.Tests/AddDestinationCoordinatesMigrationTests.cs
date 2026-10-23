using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The migration that gives the stops their optional coordinates, checked
/// without a database.
///
/// A migration that moves data is normally only exercised against a real
/// server. Its operations can also be inspected directly, which keeps the
/// suite database-free while still guarding the shape of the change.
/// </summary>
public sealed class AddDestinationCoordinatesMigrationTests
{
    private static List<MigrationOperation> UpOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Migration).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(up);
        up.Invoke(new AddDestinationCoordinates(), [builder]);

        return [.. builder.Operations];
    }

    private static List<MigrationOperation> DownOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var down = typeof(Migration).GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(down);
        down.Invoke(new AddDestinationCoordinates(), [builder]);

        return [.. builder.Operations];
    }

    [Fact]
    public void Up_AddsTheTwoCoordinatesAsNullableColumns()
    {
        var columns = UpOperations().OfType<AddColumnOperation>()
            .Where(operation => operation.Table == "TripDestinations")
            .ToArray();

        Assert.Equal(2, columns.Length);
        Assert.All(columns, column => Assert.True(column.IsNullable));
        Assert.All(columns, column => Assert.Equal("float", column.ColumnType));
        Assert.Contains(columns, column => column.Name == "Latitude");
        Assert.Contains(columns, column => column.Name == "Longitude");
    }

    [Fact]
    public void Up_AddsTheCheckConstraintOnTheCoordinates()
    {
        var constraint = UpOperations().OfType<AddCheckConstraintOperation>()
            .SingleOrDefault(candidate => candidate.Name == "CK_TripDestinations_Coordinates");

        Assert.NotNull(constraint);
        Assert.Equal("TripDestinations", constraint.Table);
        Assert.Contains("[Latitude] IS NULL AND [Longitude] IS NULL", constraint.Sql);
        Assert.Contains("[Latitude] BETWEEN -90 AND 90", constraint.Sql);
    }

    [Fact]
    public void Up_DropsNothing()
    {
        var operations = UpOperations();

        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<DropTableOperation>());
        Assert.Empty(operations.OfType<DropIndexOperation>());
        Assert.Empty(operations.OfType<DropCheckConstraintOperation>());
    }

    [Fact]
    public void Up_MovesNoData()
    {
        // The migration only adds columns. A stop that was typed by hand keeps
        // no coordinates, which reads as "not chosen from a map" rather than
        // as a place that was never named.
        Assert.Empty(UpOperations().OfType<SqlOperation>());
    }

    [Fact]
    public void Down_DropsTheConstraintAndTheTwoColumns()
    {
        var operations = DownOperations();

        Assert.Single(operations.OfType<DropCheckConstraintOperation>());
        Assert.Equal(2, operations.OfType<DropColumnOperation>().Count());
    }
}
