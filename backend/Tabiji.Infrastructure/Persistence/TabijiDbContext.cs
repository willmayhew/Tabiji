using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence.Identity;

namespace Tabiji.Infrastructure.Persistence;

/// <summary>
/// The one context, holding both the travel data and the accounts.
///
/// It is an <see cref="IdentityUserContext{TUser, TKey}"/> rather than an
/// <see cref="IdentityDbContext"/>: nothing in Tabiji authorises by Identity
/// role, so the role tables would be three tables no query can ever read. A
/// traveller's part in a trip is a domain concept that arrives with the
/// trip-membership milestone.
/// </summary>
public sealed class TabijiDbContext(DbContextOptions<TabijiDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<TripDestination> TripDestinations => Set<TripDestination>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(TabijiDbContext).Assembly);

        base.OnModelCreating(builder);
    }
}
