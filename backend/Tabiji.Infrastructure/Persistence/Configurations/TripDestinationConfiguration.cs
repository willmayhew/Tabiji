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

            // A place may not be its own parent. Deeper cycles and nesting are
            // refused by the domain, which is the authority on the hierarchy;
            // this guard catches the one case a single row can state.
            table.HasCheckConstraint(
                "CK_TripDestinations_Parent",
                "[ParentDestinationId] IS NULL OR [ParentDestinationId] <> [Id]");

            // A stop always belongs to a destination; a destination and an
            // unfiled place never do. The domain is the authority; this is the
            // single-row guard that keeps the shape honest.
            table.HasCheckConstraint(
                "CK_TripDestinations_Kind",
                "([Kind] = 'Stop' AND [ParentDestinationId] IS NOT NULL) OR " +
                "([Kind] IN ('Destination', 'Place') AND [ParentDestinationId] IS NULL)");
        });

        builder.HasKey(destination => destination.Id);

        builder.Property(destination => destination.Id)
            .ValueGeneratedNever();

        builder.Property(destination => destination.TripId)
            .IsRequired();

        // Null for a destination on the main route and for an unfiled place;
        // the destination's id for a stop within it. The domain allows exactly
        // one level of nesting.
        builder.Property(destination => destination.ParentDestinationId)
            .IsRequired(false);

        // What the place is: a destination, a stop or a legacy unfiled place.
        // Stored as a readable string, like a trip's status.
        builder.Property(destination => destination.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // A destination or a stop may carry the details of a visitable place.
        builder.Property(destination => destination.Category)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(destination => destination.Address)
            .HasMaxLength(TripDestination.AddressMaxLength)
            .IsRequired(false);

        builder.Property(destination => destination.Notes)
            .HasMaxLength(TripDestination.NotesMaxLength)
            .IsRequired(false);

        // The day of the trip the place is planned for. It is a calendar day,
        // not a count, so no arithmetic sits behind it.
        builder.Property(destination => destination.PlannedOn)
            .IsRequired(false);

        // When the place was saved or added, when that is known.
        builder.Property(destination => destination.CreatedAt)
            .IsRequired(false);

        // A derived convenience property, not a column.
        builder.Ignore(destination => destination.IsStop);

        // A destination owns its stops. The self-foreign-key is NO ACTION
        // rather than Cascade: a cascading key from Trips to TripDestinations
        // plus a cascading self-key would give SQL Server two cascade paths to
        // the same table, which it refuses. TripRepository removes the stops
        // in the same transaction that removes their destination.
        builder.HasOne<TripDestination>()
            .WithMany()
            .HasForeignKey(destination => destination.ParentDestinationId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // A scope can never hold two places in the same position. The scope is
        // the main route (Kind = Destination, ParentDestinationId is NULL),
        // one destination's stops (Kind = Stop, scoped by parent), or the
        // legacy unfiled places (Kind = Place, ParentDestinationId is NULL).
        // The Kind column keeps the two parentless scopes apart.
        builder.HasIndex(destination => new
            {
                destination.TripId,
                destination.Kind,
                destination.ParentDestinationId,
                destination.SortOrder,
            })
            .IsUnique()
            .HasFilter(null);

        // The self-foreign-key lookup. The unique index above starts with the
        // trip, so it does not cover a lookup by parent alone.
        builder.HasIndex(destination => destination.ParentDestinationId);
    }
}
