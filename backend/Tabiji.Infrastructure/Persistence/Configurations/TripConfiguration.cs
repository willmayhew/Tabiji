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

        builder.Property(trip => trip.Destination)
            .HasMaxLength(Trip.DestinationMaxLength)
            .IsRequired();

        builder.Property(trip => trip.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(trip => trip.CreatedAt)
            .IsRequired();
    }
}
