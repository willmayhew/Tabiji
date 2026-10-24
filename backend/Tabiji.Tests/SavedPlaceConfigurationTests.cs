using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence;

namespace Tabiji.Tests;

/// <summary>
/// The shape of the saved-place table, checked against the EF model.
///
/// The two relationships are the point of this file: a place goes with its
/// trip, and a place stays when the stop it was filed under goes.
/// </summary>
public sealed class SavedPlaceConfigurationTests
{
    private static TabijiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TabijiModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new TabijiDbContext(options);
    }

    private static IForeignKey? ForeignKeyTo(TabijiDbContext context, string principalTable)
    {
        var entity = context.Model.FindEntityType(typeof(SavedPlace));

        return entity?.GetForeignKeys()
            .SingleOrDefault(key => key.PrincipalEntityType.GetTableName() == principalTable);
    }

    [Fact]
    public void SavedPlace_IsMappedToItsOwnTable()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));

        Assert.NotNull(entity);
        Assert.Equal("SavedPlaces", entity.GetTableName());
    }

    [Fact]
    public void SavedPlace_IsKeyedOnItsOwnId()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));
        var key = entity?.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(nameof(SavedPlace.Id), Assert.Single(key.Properties).Name);
    }

    [Fact]
    public void SavedPlace_NameIsRequiredAndBounded()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));
        var name = entity?.FindProperty(nameof(SavedPlace.Name));

        Assert.NotNull(name);
        Assert.False(name.IsNullable);
        Assert.Equal(SavedPlace.NameMaxLength, name.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(SavedPlace.Latitude))]
    [InlineData(nameof(SavedPlace.Longitude))]
    public void SavedPlace_PositionIsRequired(string propertyName)
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));
        var property = entity?.FindProperty(propertyName);

        // A place is chosen from a map or a search, so unlike a destination it
        // always carries a position.
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal("float", property.GetColumnType());
    }

    [Fact]
    public void SavedPlace_PositionIsGuardedOnTheGlobe()
    {
        using var context = CreateContext();

        // The check constraints are not part of the runtime model, so the
        // design time model is the one that holds them.
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(SavedPlace));
        var constraint = entity?.GetCheckConstraints()
            .SingleOrDefault(candidate => candidate.Name == "CK_SavedPlaces_Coordinates");

        Assert.NotNull(constraint);
        Assert.Contains("[Latitude] BETWEEN -90 AND 90", constraint.Sql);
        Assert.Contains("[Longitude] BETWEEN -180 AND 180", constraint.Sql);
    }

    [Fact]
    public void SavedPlace_CategoryIsAnOptionalString()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));
        var category = entity?.FindProperty(nameof(SavedPlace.Category));

        // A place the reader has not sorted yet carries no category, so the
        // value is stored as a readable string, like a trip's status.
        Assert.NotNull(category);
        Assert.True(category.IsNullable);
        Assert.Equal("nvarchar(30)", category.GetColumnType());
    }

    [Theory]
    [InlineData(nameof(SavedPlace.Address), SavedPlace.AddressMaxLength)]
    [InlineData(nameof(SavedPlace.Notes), SavedPlace.NotesMaxLength)]
    public void SavedPlace_TheDescriptionAndTheNoteAreOptionalAndBounded(
        string propertyName,
        int maxLength)
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));
        var property = entity?.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.True(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    [Fact]
    public void SavedPlace_CreatedAtIsRequired()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));
        var createdAt = entity?.FindProperty(nameof(SavedPlace.CreatedAt));

        Assert.NotNull(createdAt);
        Assert.False(createdAt.IsNullable);
    }

    [Fact]
    public void SavedPlace_BelongsToOneTripAndGoesWithIt()
    {
        using var context = CreateContext();

        var key = ForeignKeyTo(context, "Trips");

        Assert.NotNull(key);
        Assert.Equal(nameof(SavedPlace.TripId), Assert.Single(key.Properties).Name);
        Assert.Equal(DeleteBehavior.Cascade, key.DeleteBehavior);
    }

    [Fact]
    public void SavedPlace_StaysWhenTheStopItIsFiledUnderGoes()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));

        // The stop association is optional: a place can be saved to a trip
        // without being filed under anywhere.
        var property = entity?.FindProperty(nameof(SavedPlace.DestinationId));
        Assert.NotNull(property);
        Assert.True(property.IsNullable);

        // And when the stop goes the place stays, with the filing cleared
        // rather than the place deleted.
        //
        // The database is not the one that clears it. A cascading key from the
        // trip and an unloading key from the stop would be two paths to the
        // same table, which SQL Server refuses, so this key is NO ACTION and
        // TripRepository clears the filing in the transaction that removes the
        // stop. The behaviour itself is proved against a real database in
        // SavedPlaceRepositoryTests.
        var key = ForeignKeyTo(context, "TripDestinations");
        Assert.NotNull(key);
        Assert.Equal(nameof(SavedPlace.DestinationId), Assert.Single(key.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior);
        Assert.False(key.IsRequired);
    }

    [Theory]
    [InlineData(nameof(SavedPlace.TripId))]
    [InlineData(nameof(SavedPlace.DestinationId))]
    public void SavedPlace_IsIndexedOnEveryForeignKey(string propertyName)
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(SavedPlace));
        var index = entity?.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Count == 1 && candidate.Properties[0].Name == propertyName);

        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }
}
