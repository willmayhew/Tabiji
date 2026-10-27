using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tabiji.Domain.Trips;

namespace Tabiji.Infrastructure.Persistence.Configurations;

public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");

        builder.HasKey(trip => trip.Id);

        builder.Property(trip => trip.Id)
            .ValueGeneratedNever();

        builder.Property(trip => trip.Name)
            .HasMaxLength(Trip.NameMaxLength)
            .IsRequired();

        builder.Property(trip => trip.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(trip => trip.CreatedAt)
            .IsRequired();

        // The route. The navigation is read-only, so the backing field is
        // named explicitly rather than left to the convention. It holds every
        // place on the trip, destinations and stops alike; the route is a
        // domain-side filter, not a second relationship.
        builder.HasMany(trip => trip.Destinations)
            .WithOne()
            .HasForeignKey(destination => destination.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(trip => trip.Destinations)
            .HasField("_destinations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Derived, read-only views of the route and the unfiled places. EF
        // would otherwise read them as extra collection navigations and invent
        // a shadow TripId.
        builder.Ignore(trip => trip.ParentDestinations);
        builder.Ignore(trip => trip.UnfiledPlaces);

        // The legacy scalar column. It held the only copy of a trip's
        // destination before the route existed. It is kept for one release so
        // that no data is lost, but nothing in the application reads or writes
        // it any more. Mapping it here is what stops the next migration from
        // dropping the column: delete these lines, and the drop is generated
        // deliberately, in its own migration, once the backfill has been
        // verified.
        builder.Property<string>("Destination")
            .HasColumnName("Destination")
            .HasMaxLength(TripDestination.NameMaxLength)
            .IsRequired(false);
    }
}
