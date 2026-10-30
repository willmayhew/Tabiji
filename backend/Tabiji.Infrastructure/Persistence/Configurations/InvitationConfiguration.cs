using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;
using Tabiji.Infrastructure.Persistence.Identity;

namespace Tabiji.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    /// <summary>
    /// The name of the filtered unique index. It is named explicitly because
    /// EF's convention would give it the same name as an unfiltered index, and
    /// the filter is the whole point.
    /// </summary>
    public const string PendingIndexName = "IX_Invitations_TripId_NormalizedEmail_Pending";

    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations", table =>
        {
            // A bad clock or a bad caller must not create an invitation that
            // was already expired when it was written.
            table.HasCheckConstraint(
                "CK_Invitations_Expiry",
                "[ExpiresAt] > [CreatedAt]");

            // The lifecycle is a closed set. The domain is the authority; this
            // keeps a hand-written row from inventing a state no code reads.
            table.HasCheckConstraint(
                "CK_Invitations_Status",
                "[Status] IN ('Pending', 'Accepted', 'Declined', 'Revoked', 'Expired')");

            // Ownership is granted by promoting an existing member, not by an
            // invitation, but the column is allowed the same vocabulary as a
            // membership so a future change needs no data rewrite.
            table.HasCheckConstraint(
                "CK_Invitations_Role",
                "[Role] IN ('Owner', 'Editor')");
        });

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Id)
            .ValueGeneratedNever();

        builder.Property(invitation => invitation.TripId)
            .ValueGeneratedNever();

        builder.Property(invitation => invitation.Email)
            .HasMaxLength(EmailAddress.MaxLength)
            .IsRequired();

        builder.Property(invitation => invitation.NormalizedEmail)
            .HasMaxLength(EmailAddress.MaxLength)
            .IsRequired();

        builder.Property(invitation => invitation.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // The token itself is never stored. Thirty-two bytes is a SHA-256.
        builder.Property(invitation => invitation.TokenHash)
            .HasColumnType("varbinary(32)")
            .IsRequired();

        builder.Property(invitation => invitation.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(invitation => invitation.CreatedAt)
            .IsRequired();

        builder.Property(invitation => invitation.ExpiresAt)
            .IsRequired();

        builder.Property(invitation => invitation.InvitedByUserId)
            .ValueGeneratedNever();

        builder.Property(invitation => invitation.AcceptedAt)
            .IsRequired(false);

        builder.Property(invitation => invitation.AcceptedByUserId)
            .IsRequired(false);

        builder.Property(invitation => invitation.DecidedAt)
            .IsRequired(false);

        builder.Property(invitation => invitation.DecidedByUserId)
            .IsRequired(false);

        // A deleted trip takes its invitations with it. Deleting an account
        // does not: the database refuses, exactly as it does for membership.
        builder.HasOne<Trip>()
            .WithMany()
            .HasForeignKey(invitation => invitation.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(invitation => invitation.DecidedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // A token resolves to exactly one invitation.
        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique();

        // One pending invitation per address per trip. The filter is what
        // allows a fresh invitation after one is declined, revoked or expired.
        builder.HasIndex(invitation => new
            {
                invitation.TripId,
                invitation.NormalizedEmail,
            })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'")
            .HasDatabaseName(PendingIndexName);

        // "Which invitations are waiting for this account" is a lookup by
        // address and status.
        builder.HasIndex(invitation => new
            {
                invitation.NormalizedEmail,
                invitation.Status,
            });
    }
}
