using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tabiji.Domain.Trips;

namespace Tabiji.Infrastructure.Persistence.Configurations;

public sealed class TripDestinationConfiguration : IEntityTypeConfiguration<TripDestination>
{
    public void Configure(EntityTypeBuilder<TripDestination> builder)
    {
        builder.ToTable("TripDestinations");

        builder.HasKey(destination => destination.Id);

        builder.Property(destination => destination.Id)
            .ValueGeneratedNever();

        builder.Property(destination => destination.TripId)
            .IsRequired();

        builder.Property(destination => destination.Name)
            .HasMaxLength(TripDestination.NameMaxLength)
            .IsRequired();

        builder.Property(destination => destination.SortOrder)
            .IsRequired();

        // A trip can never hold two stops in the same position. The index also
        // covers the foreign key lookup, so one index does both jobs.
        builder.HasIndex(destination => new { destination.TripId, destination.SortOrder })
            .IsUnique();
    }
}
