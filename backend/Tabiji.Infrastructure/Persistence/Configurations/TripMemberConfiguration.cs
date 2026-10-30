using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence.Identity;

namespace Tabiji.Infrastructure.Persistence.Configurations;

public sealed class TripMemberConfiguration : IEntityTypeConfiguration<TripMember>
{
    public void Configure(EntityTypeBuilder<TripMember> builder)
    {
        builder.ToTable("TripMembers");

        // One row per person per trip: a person cannot be a member twice, and
        // the key is what makes that true in the database rather than in code.
        builder.HasKey(member => new { member.TripId, member.UserId });

        builder.Property(member => member.TripId)
            .ValueGeneratedNever();

        builder.Property(member => member.UserId)
            .ValueGeneratedNever();

        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(member => member.GrantedAt)
            .IsRequired();

        // A deleted trip takes its membership with it. A deleted account does
        // not: stripping ownership silently would leave a trip nobody can
        // reach, so the database refuses instead and the day accounts can be
        // deleted has to decide what happens to their trips.
        builder.HasOne<Trip>()
            .WithMany()
            .HasForeignKey(member => member.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // "Which trips may this account see" is the query every request makes.
        // The composite primary key already indexes TripId first.
        builder.HasIndex(member => member.UserId);
    }
}
