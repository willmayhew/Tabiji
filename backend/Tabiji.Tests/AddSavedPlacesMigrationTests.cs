using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Tabiji.Infrastructure.Persistence.Migrations;

namespace Tabiji.Tests;

/// <summary>
/// The migration that gives a trip its saved places, checked without a
/// database.
///
/// The change is additive and moves no data, so its operations can be read
/// directly. The two delete behaviours are the interesting part: a place goes
/// with its trip, and a place outlives the stop it was filed under.
/// </summary>
public sealed class AddSavedPlacesMigrationTests
{
    private static List<MigrationOperation> UpOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Migration).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(up);
        up.Invoke(new AddSavedPlaces(), [builder]);

        return [.. builder.Operations];
    }

    private static List<MigrationOperation> DownOperations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var down = typeof(Migration).GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(down);
        down.Invoke(new AddSavedPlaces(), [builder]);

        return [.. builder.Operations];
    }

    private static CreateTableOperation SavedPlaces()
    {
        var operation = UpOperations().OfType<CreateTableOperation>()
            .SingleOrDefault(candidate => candidate.Name == "SavedPlaces");

        Assert.NotNull(operation);

        return operation;
    }

    [Fact]
    public void Up_CreatesTheSavedPlacesTable()
    {
        Assert.Equal(
            [
                "Id",
                "TripId",
                "DestinationId",
                "Name",
                "Latitude",
                "Longitude",
                "Category",
                "Address",
                "Notes",
                "CreatedAt"
            ],
            SavedPlaces().Columns.Select(column => column.Name));
    }

    [Fact]
    public void Up_MakesTheOwnershipAndThePositionRequired()
    {
        var columns = SavedPlaces().Columns.ToDictionary(column => column.Name);

        Assert.False(columns["TripId"].IsNullable);
        Assert.False(columns["Name"].IsNullable);
        Assert.False(columns["Latitude"].IsNullable);
        Assert.False(columns["Longitude"].IsNullable);
        Assert.False(columns["CreatedAt"].IsNullable);

        Assert.Equal("float", columns["Latitude"].ColumnType);
        Assert.Equal("float", columns["Longitude"].ColumnType);
        Assert.Equal("datetimeoffset", columns["CreatedAt"].ColumnType);
    }

    [Fact]
    public void Up_LeavesTheStopAssociationAndTheReadersOwnTextOptional()
    {
        var columns = SavedPlaces().Columns.ToDictionary(column => column.Name);

        Assert.True(columns["DestinationId"].IsNullable);
        Assert.True(columns["Category"].IsNullable);
        Assert.True(columns["Address"].IsNullable);
        Assert.True(columns["Notes"].IsNullable);
    }

    [Fact]
    public void Up_KeepsThePlacesOfATripWithTheTrip()
    {
        var foreignKey = SavedPlaces().ForeignKeys
            .SingleOrDefault(key => key.PrincipalTable == "Trips");

        Assert.NotNull(foreignKey);
        Assert.Equal("TripId", Assert.Single(foreignKey.Columns));
        Assert.Equal(ReferentialAction.Cascade, foreignKey.OnDelete);
    }

    [Fact]
    public void Up_LeavesTheUnlinkingOfAStopToTheApplication()
    {
        // The stop association carries a foreign key, so a place can only ever
        // name a stop that exists. It is Restrict — NO ACTION on the server —
        // rather than SET NULL: a cascading key from the trip and an unloading
        // key from the stop would be two paths to the same table, which SQL
        // Server refuses outright. TripRepository clears the filing when it
        // removes the stop, so the place still outlives it.
        var foreignKey = SavedPlaces().ForeignKeys
            .SingleOrDefault(key => key.PrincipalTable == "TripDestinations");

        Assert.NotNull(foreignKey);
        Assert.Equal("DestinationId", Assert.Single(foreignKey.Columns));
        Assert.Equal(ReferentialAction.Restrict, foreignKey.OnDelete);
    }

    [Fact]
    public void Up_GuardsThePositionOnTheGlobe()
    {
        var constraint = SavedPlaces().CheckConstraints
            .SingleOrDefault(candidate => candidate.Name == "CK_SavedPlaces_Coordinates");

        Assert.NotNull(constraint);
        Assert.Contains("[Latitude] BETWEEN -90 AND 90", constraint.Sql);
        Assert.Contains("[Longitude] BETWEEN -180 AND 180", constraint.Sql);
    }

    [Theory]
    [InlineData("IX_SavedPlaces_TripId", "TripId")]
    [InlineData("IX_SavedPlaces_DestinationId", "DestinationId")]
    public void Up_IndexesBothForeignKeys(string indexName, string column)
    {
        var index = UpOperations().OfType<CreateIndexOperation>()
            .SingleOrDefault(candidate => candidate.Name == indexName);

        Assert.NotNull(index);
        Assert.False(index.IsUnique);
        Assert.Equal(column, Assert.Single(index.Columns));
    }

    [Fact]
    public void Up_MovesNoDataAndChangesNothingThatExists()
    {
        var operations = UpOperations();

        Assert.Empty(operations.OfType<SqlOperation>());
        Assert.Empty(operations.OfType<AddColumnOperation>());
        Assert.Empty(operations.OfType<AlterColumnOperation>());
        Assert.Empty(operations.OfType<DropColumnOperation>());
        Assert.Empty(operations.OfType<DropTableOperation>());
    }

    [Fact]
    public void Down_DropsTheTable()
    {
        var operation = DownOperations().OfType<DropTableOperation>().SingleOrDefault();

        Assert.NotNull(operation);
        Assert.Equal("SavedPlaces", operation.Name);
    }
}
