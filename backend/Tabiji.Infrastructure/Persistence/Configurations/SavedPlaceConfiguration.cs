using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tabiji.Domain.Trips;

namespace Tabiji.Infrastructure.Persistence.Configurations;

public sealed class SavedPlaceConfiguration : IEntityTypeConfiguration<SavedPlace>
{
    public void Configure(EntityTypeBuilder<SavedPlace> builder)
    {
        builder.ToTable("SavedPlaces", table =>
        {
            // A saved place always has a position, and it falls inside the
            // range the globe allows. A belt-and-braces guard on a single row;
            // the domain is the authority.
            table.HasCheckConstraint(
                "CK_SavedPlaces_Coordinates",
                "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");
        });

        builder.HasKey(place => place.Id);

        builder.Property(place => place.Id)
            .ValueGeneratedNever();

        builder.Property(place => place.TripId)
            .IsRequired();

        builder.Property(place => place.Name)
            .HasMaxLength(SavedPlace.NameMaxLength)
            .IsRequired();

        // A place is chosen from a map or a search, so unlike a destination it
        // always carries both coordinates.
        builder.Property(place => place.Latitude)
            .IsRequired();

        builder.Property(place => place.Longitude)
            .IsRequired();

        builder.Property(place => place.Category)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired(false);

        // The description and the note are the reader's own, and either may
        // be left out.
        builder.Property(place => place.Address)
            .HasMaxLength(SavedPlace.AddressMaxLength)
            .IsRequired(false);

        builder.Property(place => place.Notes)
            .HasMaxLength(SavedPlace.NotesMaxLength)
            .IsRequired(false);

        builder.Property(place => place.CreatedAt)
            .IsRequired();

        // A place belongs to one trip and goes when the trip goes, exactly as
        // a trip's stops do.
        builder.HasOne<Trip>()
            .WithMany()
            .HasForeignKey(place => place.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        // A place may be filed under one of its trip's stops. The stop going
        // does not take the place with it: the place stays on the trip and
        // simply comes off the route, so nothing the reader wrote is lost
        // with the stop it happened to be filed under.
        //
        // That unlinking is done by TripRepository rather than by the
        // database. Cascading from Trips to SavedPlaces and unloading from
        // TripDestinations to SavedPlaces would give SQL Server two paths to
        // the same table from the same row, which it refuses outright: "may
        // cause cycles or multiple cascade paths". The first is kept, because
        // a place belongs to its trip; the second is NO ACTION, and the
        // repository clears the association in the same transaction that
        // removes the stop.
        builder.HasOne<TripDestination>()
            .WithMany()
            .HasForeignKey(place => place.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);

        // The list of a trip's places. The index on the stop covers the other
        // foreign key, which SQL Server does not index on its own.
        builder.HasIndex(place => place.TripId);

        builder.HasIndex(place => place.DestinationId);
    }
}
