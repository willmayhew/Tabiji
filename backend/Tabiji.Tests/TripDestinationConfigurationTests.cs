using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence;

namespace Tabiji.Tests;

/// <summary>
/// The shape of the route table, checked against the EF model.
///
/// The model is built without a connection being opened, so these tests need
/// no database. That property is the reason the whole suite is database-free,
/// and it is worth keeping.
/// </summary>
public sealed class TripDestinationConfigurationTests
{
    private static TabijiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TabijiModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new TabijiDbContext(options);
    }

    [Fact]
    public void TripDestination_IsMappedToItsOwnTable()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(TripDestination));

        Assert.NotNull(entity);
        Assert.Equal("TripDestinations", entity.GetTableName());
    }

    [Fact]
    public void TripDestination_IsKeyedOnItsOwnId()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(TripDestination));
        var key = entity?.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(nameof(TripDestination.Id), Assert.Single(key.Properties).Name);
    }

    [Fact]
    public void TripDestination_NameIsRequiredAndBounded()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(TripDestination));
        var name = entity?.FindProperty(nameof(TripDestination.Name));

        Assert.NotNull(name);
        Assert.False(name.IsNullable);
        Assert.Equal(TripDestination.NameMaxLength, name.GetMaxLength());
    }

    [Fact]
    public void TripDestination_SortOrderIsRequired()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(TripDestination));
        var sortOrder = entity?.FindProperty(nameof(TripDestination.SortOrder));

        Assert.NotNull(sortOrder);
        Assert.False(sortOrder.IsNullable);
    }

    [Fact]
    public void TripDestination_BelongsToOneTripAndIsDeletedWithIt()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(TripDestination));
        var foreignKey = entity?.GetForeignKeys().SingleOrDefault();

        Assert.NotNull(foreignKey);
        Assert.Equal(nameof(TripDestination.TripId), Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void TripDestination_CannotHoldTwoStopsInTheSamePosition()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(TripDestination));
        var index = entity?.GetIndexes().SingleOrDefault();

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal(
            [nameof(TripDestination.TripId), nameof(TripDestination.SortOrder)],
            index.Properties.Select(property => property.Name));
    }

    [Theory]
    [InlineData(nameof(TripDestination.StartsOn))]
    [InlineData(nameof(TripDestination.EndsOn))]
    public void TripDestination_DatesAreOptional(string propertyName)
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(TripDestination));
        var property = entity?.FindProperty(propertyName);

        // A stop may be added before its dates are known, and either date
        // may be left out on its own.
        Assert.NotNull(property);
        Assert.True(property.IsNullable);
        Assert.Equal("date", property.GetColumnType());
    }

    [Fact]
    public void TripDestination_CannotEndBeforeItStarts()
    {
        using var context = CreateContext();

        // The check constraints are not part of the runtime model, so the
        // design time model is the one that holds them.
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(TripDestination));
        var constraint = entity?.GetCheckConstraints()
            .SingleOrDefault(candidate => candidate.Name == "CK_TripDestinations_Dates");

        // A belt-and-braces guard on a single row. The domain is the
        // authority on this, and the rule that spans the whole route cannot
        // be written in SQL at all.
        Assert.NotNull(constraint);
        Assert.Contains("[EndsOn] >= [StartsOn]", constraint.Sql);
    }

    [Fact]
    public void Trip_KeepsTheLegacyDestinationColumnForOneRelease()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(Trip));
        var legacy = entity?.FindProperty("Destination");

        // The column is still in the model, so the next migration does not
        // drop it. It is nullable because the application no longer writes it.
        Assert.NotNull(legacy);
        Assert.True(legacy.IsNullable);
        Assert.Equal(TripDestination.NameMaxLength, legacy.GetMaxLength());
    }
}
