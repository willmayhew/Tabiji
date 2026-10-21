using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The migration that gives the stops their dates, checked without a
/// database.
///
/// A migration that moves data is normally only exercised against a real
/// server. Its operations can also be inspected directly, which keeps the
/// suite database-free while still guarding the shape of the change.
/// </summary>
public sealed class AddDestinationDatesMigrationTests
{
    private static List<MigrationOperation> UpOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Migration).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(up);
        up.Invoke(new AddDestinationDates(), [builder]);

        return [.. builder.Operations];
    }

    private static List<MigrationOperation> DownOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var down = typeof(Migration).GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(down);
        down.Invoke(new AddDestinationDates(), [builder]);

        return [.. builder.Operations];
    }

    [Fact]
    public void Up_AddsTheTwoDatesAsNullableColumns()
    {
        var columns = UpOperations().OfType<AddColumnOperation>()
            .Where(operation => operation.Table == "TripDestinations")
            .ToArray();

        Assert.Equal(2, columns.Length);
        Assert.All(columns, column => Assert.True(column.IsNullable));
        Assert.All(columns, column => Assert.Equal("date", column.ColumnType));
        Assert.Contains(columns, column => column.Name == "StartsOn");
        Assert.Contains(columns, column => column.Name == "EndsOn");
    }

    [Fact]
    public void Up_AddsTheCheckConstraintOnTheDates()
    {
        var constraint = UpOperations().OfType<AddCheckConstraintOperation>()
            .SingleOrDefault(candidate => candidate.Name == "CK_TripDestinations_Dates");

        Assert.NotNull(constraint);
        Assert.Equal("TripDestinations", constraint.Table);
        Assert.Contains("[EndsOn] >= [StartsOn]", constraint.Sql);
    }

    [Fact]
    public void Up_DropsNothing()
    {
        var operations = UpOperations();

        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<DropTableOperation>());
        Assert.Empty(operations.OfType<DropIndexOperation>());
    }

    [Fact]
    public void Up_MovesNoData()
    {
        // The migration only adds columns. A stop that has no dates yet keeps
        // none, which reads as "not decided" rather than as a made-up day.
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
