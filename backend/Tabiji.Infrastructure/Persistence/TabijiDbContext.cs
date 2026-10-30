using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence.Identity;

namespace Tabiji.Infrastructure.Persistence;

/// <summary>
/// The one context, holding both the travel data and the accounts.
///
/// It is an <see cref="IdentityUserContext{TUser, TKey}"/> rather than an
/// <see cref="IdentityDbContext"/>: nothing in Tabiji authorises by Identity
/// role, so the role tables would be three tables no query can ever read. A
/// traveller's part in a trip is a domain concept held in <see cref="TripMembers"/>,
/// where its role belongs to the trip rather than to the account.
/// </summary>
public sealed class TabijiDbContext(DbContextOptions<TabijiDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<TripDestination> TripDestinations => Set<TripDestination>();

    public DbSet<TripMember> TripMembers => Set<TripMember>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(TabijiDbContext).Assembly);

        base.OnModelCreating(builder);
    }
}
