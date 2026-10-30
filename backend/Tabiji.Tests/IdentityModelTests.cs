using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence;
using Tabiji.Infrastructure.Persistence.Identity;

namespace Tabiji.Tests;

/// <summary>
/// The shape of the account tables, checked against the EF model.
///
/// The model is built without a connection being opened, so these tests need
/// no database, exactly like the route-table tests. The point of them is that
/// the accounts were added beside the travel data, not through it.
/// </summary>
public sealed class IdentityModelTests
{
    private static readonly string[] ExpectedTables =
    [
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserTokens",
        "AspNetUsers",
        "Invitations",
        "TripDestinations",
        "TripMembers",
        "Trips"
    ];

    private static readonly string[] RoleTables =
    [
        "AspNetRoleClaims",
        "AspNetRoles",
        "AspNetUserRoles"
    ];

    private static TabijiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TabijiDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TabijiModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new TabijiDbContext(options);
    }

    private static string[] TableNames(TabijiDbContext context) =>
    [
        .. context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .OfType<string>()
            .Order(StringComparer.Ordinal)
    ];

    [Fact]
    public void Model_HoldsTheTravelTablesAndExactlyFourAccountTables()
    {
        using var context = CreateContext();

        Assert.Equal([.. ExpectedTables.Order(StringComparer.Ordinal)], TableNames(context));
    }

    [Fact]
    public void Model_AddsNoRoleTables()
    {
        using var context = CreateContext();

        var tables = TableNames(context);

        // Nothing authorises by Identity role: a traveller's part in a trip is
        // a domain rule that arrives with membership, so three role tables
        // nothing can query are not created.
        Assert.All(RoleTables, table => Assert.DoesNotContain(table, tables));
    }

    [Fact]
    public void AppUser_IsKeyedOnAGuidInItsOwnTable()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(AppUser));

        Assert.NotNull(entity);
        Assert.Equal("AspNetUsers", entity.GetTableName());

        var key = entity.FindPrimaryKey();

        Assert.NotNull(key);

        var id = Assert.Single(key.Properties);

        // A Guid key keeps the accounts consistent with every other key in the
        // database, and keeps a future membership row eight bytes wide instead
        // of a four-hundred-and-fifty character string.
        Assert.Equal(nameof(AppUser.Id), id.Name);
        Assert.Equal(typeof(Guid), id.ClrType);
        Assert.Equal("uniqueidentifier", id.GetColumnType());
    }

    [Fact]
    public void AppUser_CanBeFoundByItsNormalisedEmail()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(AppUser));

        Assert.NotNull(entity);

        var index = entity.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Count == 1 &&
                candidate.Properties[0].Name == nameof(AppUser.NormalizedEmail));

        Assert.NotNull(index);
        Assert.Equal("EmailIndex", index.GetDatabaseName());

        // One address may only register once, but that is the user validator's
        // rule rather than a unique constraint.
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void AddingTheAccountsLeavesTheTravelTablesAsTheyWere()
    {
        using var context = CreateContext();

        var trip = context.Model.FindEntityType(typeof(Trip));
        var destination = context.Model.FindEntityType(typeof(TripDestination));

        Assert.NotNull(trip);
        Assert.NotNull(destination);
        Assert.Equal("Trips", trip.GetTableName());
        Assert.Equal("TripDestinations", destination.GetTableName());

        // A trip still owns its destinations and nothing else: the account is
        // reached through a membership row, not threaded through the trip.
        Assert.Empty(trip.GetForeignKeys());

        var toTrip = destination.GetForeignKeys()
            .SingleOrDefault(key => key.PrincipalEntityType.ClrType == typeof(Trip));

        Assert.NotNull(toTrip);
        Assert.Equal(DeleteBehavior.Cascade, toTrip.DeleteBehavior);
    }

    [Fact]
    public void TripMember_IsKeyedOnTheTripAndTheAccount()
    {
        using var context = CreateContext();

        var member = context.Model.FindEntityType(typeof(TripMember));

        Assert.NotNull(member);
        Assert.Equal("TripMembers", member.GetTableName());

        var key = member.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(
            [nameof(TripMember.TripId), nameof(TripMember.UserId)],
            key.Properties.Select(property => property.Name));

        // Both halves are the same width as every other key in the database,
        // which is why the account key is a Guid rather than Identity's
        // default string.
        Assert.All(key.Properties, property => Assert.Equal(typeof(Guid), property.ClrType));
        Assert.All(key.Properties, property => Assert.Equal("uniqueidentifier", property.GetColumnType()));
        Assert.All(key.Properties, property => Assert.False(property.IsNullable));

        // The key is what makes a person one member of a trip, once.
        Assert.Equal(2, key.Properties.Count);
    }

    [Fact]
    public void TripMember_GivesAPersonOneRoleOnATrip()
    {
        using var context = CreateContext();

        var member = context.Model.FindEntityType(typeof(TripMember));

        Assert.NotNull(member);

        var role = member.FindProperty(nameof(TripMember.Role));

        Assert.NotNull(role);

        // Roles are stored as their names, like every other enum in the
        // database, so a row can be read without knowing the numbering.
        Assert.Equal(typeof(TripMemberRole), role.ClrType);
        Assert.Equal("nvarchar(20)", role.GetColumnType());
        Assert.False(role.IsNullable);

        var grantedAt = member.FindProperty(nameof(TripMember.GrantedAt));

        Assert.NotNull(grantedAt);
        Assert.Equal("datetimeoffset", grantedAt.GetColumnType());
        Assert.False(grantedAt.IsNullable);
    }

    [Fact]
    public void TripMember_BelongsToItsTripAndItsAccount()
    {
        using var context = CreateContext();

        var member = context.Model.FindEntityType(typeof(TripMember));

        Assert.NotNull(member);

        var foreignKeys = member.GetForeignKeys();

        Assert.Equal(2, foreignKeys.Count());

        var toTrip = foreignKeys.Single(
            key => key.PrincipalEntityType.ClrType == typeof(Trip));

        // Deleting a trip deletes its membership with it.
        Assert.Equal(DeleteBehavior.Cascade, toTrip.DeleteBehavior);

        var toAccount = foreignKeys.Single(
            key => key.PrincipalEntityType.ClrType == typeof(AppUser));

        // Deleting an account must not quietly strip a trip of its owner: the
        // database refuses, and the day accounts can be deleted has to decide
        // what happens to their trips.
        Assert.Equal(DeleteBehavior.NoAction, toAccount.DeleteBehavior);
    }

    [Fact]
    public void TripMember_CanBeFoundByAccount()
    {
        using var context = CreateContext();

        var member = context.Model.FindEntityType(typeof(TripMember));

        Assert.NotNull(member);

        var index = member.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.Properties.Count == 1 &&
                candidate.Properties[0].Name == nameof(TripMember.UserId));

        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Invitation_IsKeyedOnItsOwnIdAndKeepsOnlyTheTokenHash()
    {
        using var context = CreateContext();

        var invitation = context.Model.FindEntityType(typeof(Invitation));

        Assert.NotNull(invitation);
        Assert.Equal("Invitations", invitation.GetTableName());

        var key = invitation.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(nameof(Invitation.Id), Assert.Single(key.Properties).Name);

        var hash = invitation.FindProperty(nameof(Invitation.TokenHash));

        Assert.NotNull(hash);
        Assert.Equal(typeof(byte[]), hash.ClrType);
        Assert.Equal("varbinary(32)", hash.GetColumnType());
        Assert.False(hash.IsNullable);

        // The token is a bearer credential; only its hash belongs in the
        // database.
        Assert.Null(invitation.FindProperty("Token"));
    }

    [Fact]
    public void Invitation_KeepsTheAddressRoleAndStatusAsReadableValues()
    {
        using var context = CreateContext();

        var invitation = context.Model.FindEntityType(typeof(Invitation));

        Assert.NotNull(invitation);

        var email = invitation.FindProperty(nameof(Invitation.Email));
        var normalized = invitation.FindProperty(nameof(Invitation.NormalizedEmail));
        var role = invitation.FindProperty(nameof(Invitation.Role));
        var status = invitation.FindProperty(nameof(Invitation.Status));

        Assert.NotNull(email);
        Assert.NotNull(normalized);
        Assert.NotNull(role);
        Assert.NotNull(status);

        Assert.Equal(EmailAddress.MaxLength, email.GetMaxLength());
        Assert.Equal(EmailAddress.MaxLength, normalized.GetMaxLength());
        Assert.False(email.IsNullable);
        Assert.False(normalized.IsNullable);

        Assert.Equal(typeof(TripMemberRole), role.ClrType);
        Assert.Equal("nvarchar(20)", role.GetColumnType());
        Assert.False(role.IsNullable);

        Assert.Equal(typeof(InvitationStatus), status.ClrType);
        Assert.Equal("nvarchar(20)", status.GetColumnType());
        Assert.False(status.IsNullable);
    }

    [Fact]
    public void Invitation_BelongsToItsTripAndItsAccounts()
    {
        using var context = CreateContext();

        var invitation = context.Model.FindEntityType(typeof(Invitation));

        Assert.NotNull(invitation);

        var foreignKeys = invitation.GetForeignKeys();

        Assert.Equal(4, foreignKeys.Count());

        var toTrip = foreignKeys.Single(
            key => key.PrincipalEntityType.ClrType == typeof(Trip));

        // A deleted trip takes its invitations with it.
        Assert.Equal(DeleteBehavior.Cascade, toTrip.DeleteBehavior);

        var toAccounts = foreignKeys
            .Where(key => key.PrincipalEntityType.ClrType == typeof(AppUser))
            .ToArray();

        // The sender, the acceptor and the decider. None may cascade: deleting
        // an account must not quietly erase the record of an invitation.
        Assert.Equal(3, toAccounts.Length);
        Assert.All(toAccounts, key => Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior));
        Assert.Equal(
            [
                nameof(Invitation.AcceptedByUserId),
                nameof(Invitation.DecidedByUserId),
                nameof(Invitation.InvitedByUserId),
            ],
            toAccounts.SelectMany(key => key.Properties.Select(property => property.Name)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Invitation_AllowsOnePendingOfferPerAddressAndTrip()
    {
        using var context = CreateContext();

        var invitation = context.Model.FindEntityType(typeof(Invitation));

        Assert.NotNull(invitation);

        var index = invitation.GetIndexes()
            .SingleOrDefault(candidate => candidate.IsUnique && candidate.GetFilter() is not null);

        Assert.NotNull(index);
        Assert.Equal("IX_Invitations_TripId_NormalizedEmail_Pending", index.GetDatabaseName());
        Assert.Equal(
            [nameof(Invitation.TripId), nameof(Invitation.NormalizedEmail)],
            index.Properties.Select(property => property.Name));

        // The filter is what lets a fresh invitation follow a decline, a
        // revocation or an expiry.
        Assert.Equal("[Status] = 'Pending'", index.GetFilter());
    }

    [Fact]
    public void Invitation_ResolvesATokenOnce()
    {
        using var context = CreateContext();

        var invitation = context.Model.FindEntityType(typeof(Invitation));

        Assert.NotNull(invitation);

        var index = invitation.GetIndexes()
            .SingleOrDefault(candidate =>
                candidate.IsUnique &&
                candidate.Properties.Count == 1 &&
                candidate.Properties[0].Name == nameof(Invitation.TokenHash));

        Assert.NotNull(index);
    }

    [Fact]
    public void Invitation_StatesTheLifecycleInTheDatabase()
    {
        using var context = CreateContext();

        var model = context.GetService<IDesignTimeModel>().Model;
        var invitation = model.FindEntityType(typeof(Invitation));

        Assert.NotNull(invitation);

        var constraints = invitation.GetCheckConstraints()
            .ToDictionary(
                constraint => constraint.Name!,
                constraint => constraint.Sql ?? string.Empty);

        Assert.Contains("CK_Invitations_Expiry", constraints.Keys);
        Assert.Contains("[ExpiresAt] > [CreatedAt]", constraints["CK_Invitations_Expiry"]);

        Assert.Contains("CK_Invitations_Status", constraints.Keys);
        Assert.Contains("'Pending'", constraints["CK_Invitations_Status"]);
        Assert.Contains("'Expired'", constraints["CK_Invitations_Status"]);

        Assert.Contains("CK_Invitations_Role", constraints.Keys);
        Assert.Contains("'Owner'", constraints["CK_Invitations_Role"]);
        Assert.Contains("'Editor'", constraints["CK_Invitations_Role"]);
    }
}
