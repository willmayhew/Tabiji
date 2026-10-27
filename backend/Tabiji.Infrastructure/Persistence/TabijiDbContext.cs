using Microsoft.EntityFrameworkCore;
using Tabiji.Domain.Trips;

namespace Tabiji.Infrastructure.Persistence;

public sealed class TabijiDbContext(DbContextOptions<TabijiDbContext> options) : DbContext(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<TripDestination> TripDestinations => Set<TripDestination>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TabijiDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
