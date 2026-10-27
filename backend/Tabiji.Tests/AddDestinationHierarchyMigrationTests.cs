using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The migration that lets a destination hold stops, checked without a
/// database.
///
/// The change is additive and moves no data: every existing row keeps a null
/// parent, so it becomes a destination on the main route. The interesting
/// parts are the self-key's NO ACTION behaviour, the composite unique index
/// and the guard that stops a rollback from silently removing nested stops.
/// </summary>
public sealed class AddDestinationHierarchyMigrationTests
{
    private static readonly string[] ScopeColumns =
    [
        "TripId",
        "ParentDestinationId",
        "SortOrder",
    ];

    private static List<MigrationOperation> UpOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Migration).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(up);
        up.Invoke(new AddDestinationHierarchy(), [builder]);

        return [.. builder.Operations];
    }

    private static List<MigrationOperation> DownOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var down = typeof(Migration).GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(down);
        down.Invoke(new AddDestinationHierarchy(), [builder]);

        return [.. builder.Operations];
    }

    [Fact]
    public void Up_AddsOneNullableParentColumn()
    {
        var operation = Assert.Single(UpOperations().OfType<AddColumnOperation>());

        Assert.Equal("TripDestinations", operation.Table);
        Assert.Equal("ParentDestinationId", operation.Name);
        Assert.Equal("uniqueidentifier", operation.ColumnType);
        Assert.True(operation.IsNullable);
    }

    [Fact]
    public void Up_MovesNoData()
    {
        var operations = UpOperations();

        Assert.Empty(operations.OfType<SqlOperation>());
        Assert.Empty(operations.OfType<InsertDataOperation>());
        Assert.Empty(operations.OfType<UpdateDataOperation>());
        Assert.Empty(operations.OfType<DeleteDataOperation>());
        Assert.Empty(operations.OfType<AlterColumnOperation>());
        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<DropTableOperation>());
    }

    [Fact]
    public void Up_ReplacesTheRouteIndexWithOneUniqueIndexPerScope()
    {
        var operations = UpOperations();

        var dropped = Assert.Single(operations.OfType<DropIndexOperation>());
        Assert.Equal("TripDestinations", dropped.Table);
        Assert.Equal("IX_TripDestinations_TripId_SortOrder", dropped.Name);

        var scope = operations.OfType<CreateIndexOperation>()
            .Single(operation => operation.Name == "IX_TripDestinations_TripId_ParentDestinationId_SortOrder");

        Assert.Equal("TripDestinations", scope.Table);
        Assert.True(scope.IsUnique);
        Assert.Null(scope.Filter);
        Assert.Equal(ScopeColumns, scope.Columns);

        var parent = operations.OfType<CreateIndexOperation>()
            .Single(operation => operation.Name == "IX_TripDestinations_ParentDestinationId");

        Assert.False(parent.IsUnique);
        Assert.Equal("ParentDestinationId", Assert.Single(parent.Columns));
    }

    [Fact]
    public void Up_AddsASelfKeyThatDoesNotCascade()
    {
        var foreignKey = UpOperations().OfType<AddForeignKeyOperation>()
            .Single(operation => operation.Name == "FK_TripDestinations_TripDestinations_ParentDestinationId");

        Assert.Equal("TripDestinations", foreignKey.Table);
        Assert.Equal("ParentDestinationId", Assert.Single(foreignKey.Columns));
        Assert.Equal("TripDestinations", foreignKey.PrincipalTable);
        Assert.Equal("Id", Assert.Single(foreignKey.PrincipalColumns ?? []));
        Assert.Equal(ReferentialAction.Restrict, foreignKey.OnDelete);
    }

    [Fact]
    public void Up_GuardsAgainstAPlaceBeingItsOwnParent()
    {
        var constraint = Assert.Single(UpOperations().OfType<AddCheckConstraintOperation>());

        Assert.Equal("CK_TripDestinations_Parent", constraint.Name);
        Assert.Equal("TripDestinations", constraint.Table);
        Assert.Contains("[ParentDestinationId] IS NULL", constraint.Sql);
        Assert.Contains("[ParentDestinationId] <> [Id]", constraint.Sql);
    }

    [Fact]
    public void Down_RefusesWhileNestedStopsExistAndThenRevertsEverything()
    {
        var operations = DownOperations();

        var guard = Assert.Single(operations.OfType<SqlOperation>());

        Assert.Contains("ParentDestinationId", guard.Sql);
        Assert.Contains("THROW", guard.Sql);

        var selfKey = Assert.Single(operations.OfType<DropForeignKeyOperation>());
        Assert.Equal("FK_TripDestinations_TripDestinations_ParentDestinationId", selfKey.Name);

        var droppedIndexes = operations.OfType<DropIndexOperation>().Select(operation => operation.Name).ToArray();
        Assert.Contains("IX_TripDestinations_ParentDestinationId", droppedIndexes);
        Assert.Contains("IX_TripDestinations_TripId_ParentDestinationId_SortOrder", droppedIndexes);

        var column = Assert.Single(operations.OfType<DropColumnOperation>());
        Assert.Equal("ParentDestinationId", column.Name);

        var oldIndex = Assert.Single(operations.OfType<CreateIndexOperation>());
        Assert.Equal("IX_TripDestinations_TripId_SortOrder", oldIndex.Name);
        Assert.True(oldIndex.IsUnique);
    }
}
