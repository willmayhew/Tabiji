using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The migration that lets a place be planned for a day, checked without a
/// database.
///
/// The change is additive and moves no data, which is the point of it: a place
/// that existed before it comes out of it unplanned, which is the honest
/// reading. No day is invented for a place whose reader never chose one, and
/// nothing already stored is rewritten.
/// </summary>
public sealed class AddSavedPlacePlannedOnMigrationTests
{
    private static List<MigrationOperation> UpOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Migration).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(up);
        up.Invoke(new AddSavedPlacePlannedOn(), [builder]);

        return [.. builder.Operations];
    }

    private static List<MigrationOperation> DownOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var down = typeof(Migration).GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(down);
        down.Invoke(new AddSavedPlacePlannedOn(), [builder]);

        return [.. builder.Operations];
    }

    [Fact]
    public void Up_AddsOneNullableDateColumnToThePlaces()
    {
        var operation = Assert.Single(UpOperations().OfType<AddColumnOperation>());

        Assert.Equal("SavedPlaces", operation.Table);
        Assert.Equal("PlannedOn", operation.Name);
        Assert.Equal("date", operation.ColumnType);
        Assert.True(operation.IsNullable);
    }

    [Fact]
    public void Up_MovesNoDataAndChangesNothingElse()
    {
        var operations = UpOperations();

        // One operation, and no other kind: the column can be added to a table
        // that already holds places without touching a row.
        Assert.Single(operations);
        Assert.Empty(operations.OfType<SqlOperation>());
        Assert.Empty(operations.OfType<AlterColumnOperation>());
        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<CreateTableOperation>());
        Assert.Empty(operations.OfType<DropTableOperation>());
        Assert.Empty(operations.OfType<CreateIndexOperation>());
    }

    [Fact]
    public void Down_DropsTheColumn()
    {
        var operation = Assert.Single(DownOperations().OfType<DropColumnOperation>());

        Assert.Equal("SavedPlaces", operation.Table);
        Assert.Equal("PlannedOn", operation.Name);
    }
}
