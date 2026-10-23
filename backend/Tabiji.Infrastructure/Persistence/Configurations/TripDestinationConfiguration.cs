using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tabiji.Domain.Trips;

namespace Tabiji.Infrastructure.Persistence.Configurations;

public sealed class TripDestinationConfiguration : IEntityTypeConfiguration<TripDestination>
{
    public void Configure(EntityTypeBuilder<TripDestination> builder)
    {
        builder.ToTable("TripDestinations", table =>
        {
            // A belt-and-braces guard on a single row: a stop may not end
            // before it starts. The domain is the authority on this, and the
            // rule that spans the whole route cannot be written in SQL at all.
            table.HasCheckConstraint(
                "CK_TripDestinations_Dates",
                "[StartsOn] IS NULL OR [EndsOn] IS NULL OR [EndsOn] >= [StartsOn]");

            // A place chosen from a map carries both coordinates or neither,
            // and each falls inside the range the globe allows. A place typed
            // by hand carries neither. Another single-row guard; the domain is
            // the authority.
            table.HasCheckConstraint(
                "CK_TripDestinations_Coordinates",
                "([Latitude] IS NULL AND [Longitude] IS NULL) OR " +
                "([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND " +
                "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
        });

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

        // The planned stay at the stop. Either date may be left out: a stop
        // can be added long before its dates are known.
        builder.Property(destination => destination.StartsOn)
            .IsRequired(false);

        builder.Property(destination => destination.EndsOn)
            .IsRequired(false);

        // Where the place is, when it was chosen from a map. A place typed by
        // hand carries neither, which is always valid.
        builder.Property(destination => destination.Latitude)
            .IsRequired(false);

        builder.Property(destination => destination.Longitude)
            .IsRequired(false);

        // A trip can never hold two stops in the same position. The index also
        // covers the foreign key lookup, so one index does both jobs.
        builder.HasIndex(destination => new { destination.TripId, destination.SortOrder })
            .IsUnique();
    }
}
