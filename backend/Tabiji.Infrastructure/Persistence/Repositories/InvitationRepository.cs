using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tabiji.Application.Membership;
using Tabiji.Domain.Invitations;
using Tabiji.Domain.Membership;

namespace Tabiji.Infrastructure.Persistence.Repositories;

/// <summary>
/// Invitations, read and decided.
///
/// The raw token never reaches this class: the service hashes it, and every
/// lookup and every decision is made against that hash. Acceptance, decline
/// and expiry are conditional updates on the row's status, so two callers
/// cannot both win, and membership is written in the same transaction as the
/// acceptance that grants it.
///
/// The address is matched against <c>AspNetUsers.NormalizedEmail</c> in SQL.
/// No identity type crosses into Application, and a caller cannot learn
/// whether an address has an account from the shape of a response.
/// </summary>
public sealed class InvitationRepository(TabijiDbContext dbContext) : IInvitationRepository
{
    private static readonly int[] DuplicateKeyNumbers = [2601, 2627];

    public async Task<CreateInvitationResult> CreateAsync(
        Guid tripId,
        UserId actor,
        EmailAddress email,
        byte[] tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var actorRole = await ViewerRoleAsync(tripId, actor, cancellationToken);

        if (actorRole is null)
        {
            return new CreateInvitationResult(InvitationCreateOutcome.NotFound);
        }

        if (actorRole != TripMemberRole.Owner)
        {
            return new CreateInvitationResult(InvitationCreateOutcome.Forbidden);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // A pending invitation that has run out of time must not block the
        // fresh one the owner is trying to send. Marking it expired frees the
        // filtered unique index.
        await dbContext.Invitations
            .Where(invitation =>
                invitation.TripId == tripId &&
                invitation.NormalizedEmail == email.Normalized &&
                invitation.Status == InvitationStatus.Pending &&
                invitation.ExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(invitation => invitation.Status, InvitationStatus.Expired)
                    .SetProperty(invitation => invitation.DecidedAt, (DateTimeOffset?)now),
                cancellationToken);

        var alreadyMember = await (
                from member in dbContext.TripMembers
                join account in dbContext.Users on member.UserId equals account.Id
                where member.TripId == tripId && account.NormalizedEmail == email.Normalized
                select member.UserId)
            .AnyAsync(cancellationToken);

        if (alreadyMember)
        {
            return new CreateInvitationResult(InvitationCreateOutcome.AlreadyMember);
        }

        var alreadyInvited = await dbContext.Invitations
            .AnyAsync(
                invitation =>
                    invitation.TripId == tripId &&
                    invitation.NormalizedEmail == email.Normalized &&
                    invitation.Status == InvitationStatus.Pending,
                cancellationToken);

        if (alreadyInvited)
        {
            return new CreateInvitationResult(InvitationCreateOutcome.AlreadyInvited);
        }

        var invitation = Invitation.Issue(
            tripId,
            email,
            TripMemberRole.Editor,
            tokenHash,
            actor.Value,
            now,
            expiresAt);

        dbContext.Invitations.Add(invitation);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicate(exception))
        {
            // Another owner invited the same address between the check and the
            // insert. The filtered unique index is the real guard.
            return new CreateInvitationResult(InvitationCreateOutcome.AlreadyInvited);
        }

        await transaction.CommitAsync(cancellationToken);

        var invitedByEmail = await AccountEmailAsync(actor.Value, cancellationToken);

        return new CreateInvitationResult(
            InvitationCreateOutcome.Created,
            new TripInvitationResponse(
                invitation.Id,
                invitation.TripId,
                invitation.Email,
                invitation.Role,
                invitation.EffectiveStatus(now),
                invitation.CreatedAt,
                invitation.ExpiresAt,
                invitedByEmail));
    }

    public async Task<InvitationListResult> ListForTripAsync(
        Guid tripId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var viewerRole = await ViewerRoleAsync(tripId, viewer, cancellationToken);

        if (viewerRole is null)
        {
            return new InvitationListResult(InvitationListOutcome.NotFound, []);
        }

        if (viewerRole != TripMemberRole.Owner)
        {
            return new InvitationListResult(InvitationListOutcome.Forbidden, []);
        }

        var rows = await (
                from invitation in dbContext.Invitations.AsNoTracking()
                join account in dbContext.Users on invitation.InvitedByUserId equals account.Id
                where invitation.TripId == tripId
                orderby invitation.CreatedAt descending
                select new
                {
                    Invitation = invitation,
                    InvitedByEmail = account.Email ?? account.UserName ?? string.Empty,
                })
            .ToListAsync(cancellationToken);

        var invitations = rows
            .Select(row => new TripInvitationResponse(
                row.Invitation.Id,
                row.Invitation.TripId,
                row.Invitation.Email,
                row.Invitation.Role,
                row.Invitation.EffectiveStatus(now),
                row.Invitation.CreatedAt,
                row.Invitation.ExpiresAt,
                row.InvitedByEmail))
            .ToArray();

        return new InvitationListResult(InvitationListOutcome.Listed, invitations);
    }

    public async Task<InvitationActionResult> RevokeAsync(
        Guid tripId,
        UserId actor,
        Guid invitationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var actorRole = await ViewerRoleAsync(tripId, actor, cancellationToken);

        if (actorRole is null)
        {
            return new InvitationActionResult(InvitationActionOutcome.NotFound);
        }

        if (actorRole != TripMemberRole.Owner)
        {
            return new InvitationActionResult(InvitationActionOutcome.Forbidden);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affected = await dbContext.Invitations
            .Where(invitation =>
                invitation.Id == invitationId &&
                invitation.TripId == tripId &&
                invitation.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(invitation => invitation.Status, InvitationStatus.Revoked)
                    .SetProperty(invitation => invitation.DecidedAt, (DateTimeOffset?)now)
                    .SetProperty(invitation => invitation.DecidedByUserId, (Guid?)actor.Value),
                cancellationToken);

        if (affected == 1)
        {
            await transaction.CommitAsync(cancellationToken);
            return new InvitationActionResult(InvitationActionOutcome.Changed);
        }

        // Not there, or no longer pending: both are the same answer to an
        // owner, and neither reveals a token.
        return new InvitationActionResult(InvitationActionOutcome.NotFound);
    }

    public async Task<InvitationPreviewResult> PreviewAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var email = await CallerEmailAsync(viewer, cancellationToken);

        if (email is null)
        {
            return new InvitationPreviewResult(InvitationPreviewOutcome.NotFound);
        }

        var row = await (
                from invitation in dbContext.Invitations.AsNoTracking()
                join trip in dbContext.Trips on invitation.TripId equals trip.Id
                join account in dbContext.Users on invitation.InvitedByUserId equals account.Id
                where invitation.TokenHash == tokenHash && invitation.NormalizedEmail == email
                select new
                {
                    Invitation = invitation,
                    trip.Name,
                    InvitedByEmail = account.Email ?? account.UserName ?? string.Empty,
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            // An unknown token and a token for another address are the same
            // answer, so the link cannot be used to discover an address.
            return new InvitationPreviewResult(InvitationPreviewOutcome.NotFound);
        }

        var status = row.Invitation.EffectiveStatus(now);

        return new InvitationPreviewResult(
            InvitationPreviewOutcome.Found,
            new InvitationPreviewResponse(
                row.Invitation.TripId,
                row.Name,
                row.InvitedByEmail,
                row.Invitation.Role,
                status,
                row.Invitation.ExpiresAt,
                status == InvitationStatus.Pending));
    }

    public async Task<AcceptInvitationResult> AcceptAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var email = await CallerEmailAsync(viewer, cancellationToken);

        if (email is null)
        {
            return new AcceptInvitationResult(InvitationAcceptOutcome.NotFound);
        }

        var invitation = await FindByTokenAsync(tokenHash, email, cancellationToken);

        return invitation is null
            ? new AcceptInvitationResult(InvitationAcceptOutcome.NotFound)
            : await AcceptCoreAsync(invitation, viewer, now, cancellationToken);
    }

    public async Task<AcceptInvitationResult> AcceptByIdAsync(
        Guid invitationId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var email = await CallerEmailAsync(viewer, cancellationToken);

        if (email is null)
        {
            return new AcceptInvitationResult(InvitationAcceptOutcome.NotFound);
        }

        var invitation = await FindByIdAsync(invitationId, email, cancellationToken);

        return invitation is null
            ? new AcceptInvitationResult(InvitationAcceptOutcome.NotFound)
            : await AcceptCoreAsync(invitation, viewer, now, cancellationToken);
    }

    public async Task<InvitationActionResult> DeclineAsync(
        byte[] tokenHash,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var email = await CallerEmailAsync(viewer, cancellationToken);

        if (email is null)
        {
            return new InvitationActionResult(InvitationActionOutcome.NotFound);
        }

        var invitation = await FindByTokenAsync(tokenHash, email, cancellationToken);

        return invitation is null
            ? new InvitationActionResult(InvitationActionOutcome.NotFound)
            : await DeclineCoreAsync(invitation, viewer, now, cancellationToken);
    }

    public async Task<InvitationActionResult> DeclineByIdAsync(
        Guid invitationId,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var email = await CallerEmailAsync(viewer, cancellationToken);

        if (email is null)
        {
            return new InvitationActionResult(InvitationActionOutcome.NotFound);
        }

        var invitation = await FindByIdAsync(invitationId, email, cancellationToken);

        return invitation is null
            ? new InvitationActionResult(InvitationActionOutcome.NotFound)
            : await DeclineCoreAsync(invitation, viewer, now, cancellationToken);
    }

    public async Task<IReadOnlyList<RecipientInvitationResponse>> ListForRecipientAsync(
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var email = await CallerEmailAsync(viewer, cancellationToken);

        if (email is null)
        {
            return [];
        }

        return await (
                from invitation in dbContext.Invitations.AsNoTracking()
                join trip in dbContext.Trips on invitation.TripId equals trip.Id
                join account in dbContext.Users on invitation.InvitedByUserId equals account.Id
                where invitation.NormalizedEmail == email &&
                      invitation.Status == InvitationStatus.Pending &&
                      invitation.ExpiresAt > now
                orderby invitation.ExpiresAt
                select new RecipientInvitationResponse(
                    invitation.Id,
                    invitation.TripId,
                    trip.Name,
                    account.Email ?? account.UserName ?? string.Empty,
                    invitation.Role,
                    invitation.Status,
                    invitation.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Performs the one conditional update that decides the acceptance, then
    /// writes the membership in the same transaction. Only the caller that
    /// changed the pending row inserts a membership, so a second acceptance
    /// cannot duplicate it.
    /// </summary>
    private async Task<AcceptInvitationResult> AcceptCoreAsync(
        Invitation invitation,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affected = await dbContext.Invitations
            .Where(candidate =>
                candidate.Id == invitation.Id &&
                candidate.Status == InvitationStatus.Pending &&
                candidate.ExpiresAt > now &&
                candidate.NormalizedEmail == invitation.NormalizedEmail)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Status, InvitationStatus.Accepted)
                    .SetProperty(candidate => candidate.AcceptedAt, (DateTimeOffset?)now)
                    .SetProperty(candidate => candidate.AcceptedByUserId, (Guid?)viewer.Value),
                cancellationToken);

        if (affected == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ClassifyAcceptAsync(invitation.Id, now, cancellationToken);
        }

        var existingRole = await dbContext.TripMembers
            .Where(member => member.TripId == invitation.TripId && member.UserId == viewer.Value)
            .Select(member => (TripMemberRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);

        var role = existingRole ?? invitation.Role;

        if (existingRole is null)
        {
            dbContext.TripMembers.Add(
                TripMember.Grant(invitation.TripId, viewer, invitation.Role, now));

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicate(exception))
            {
                // A concurrent path created the membership. It exists, which
                // is all acceptance needed; the role it already has stands.
                dbContext.ChangeTracker.Clear();
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return new AcceptInvitationResult(
            InvitationAcceptOutcome.Accepted,
            new AcceptedInvitation(invitation.TripId, role));
    }

    private async Task<InvitationActionResult> DeclineCoreAsync(
        Invitation invitation,
        UserId viewer,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affected = await dbContext.Invitations
            .Where(candidate =>
                candidate.Id == invitation.Id &&
                candidate.Status == InvitationStatus.Pending &&
                candidate.ExpiresAt > now &&
                candidate.NormalizedEmail == invitation.NormalizedEmail)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Status, InvitationStatus.Declined)
                    .SetProperty(candidate => candidate.DecidedAt, (DateTimeOffset?)now)
                    .SetProperty(candidate => candidate.DecidedByUserId, (Guid?)viewer.Value),
                cancellationToken);

        if (affected == 1)
        {
            await transaction.CommitAsync(cancellationToken);
            return new InvitationActionResult(InvitationActionOutcome.Changed);
        }

        await transaction.RollbackAsync(cancellationToken);

        return await ClassifyActionAsync(invitation.Id, now, cancellationToken);
    }

    /// <summary>
    /// Explains why an acceptance lost. A pending row past its expiry is
    /// recorded as expired; a decided row reports which decision it was.
    /// </summary>
    private async Task<AcceptInvitationResult> ClassifyAcceptAsync(
        Guid invitationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await dbContext.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);

        if (invitation is null)
        {
            return new AcceptInvitationResult(InvitationAcceptOutcome.NotFound);
        }

        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now)
        {
            await ExpireAsync(invitationId, now, cancellationToken);
            return new AcceptInvitationResult(InvitationAcceptOutcome.Expired);
        }

        var outcome = invitation.Status switch
        {
            InvitationStatus.Accepted => InvitationAcceptOutcome.AlreadyUsed,
            InvitationStatus.Declined => InvitationAcceptOutcome.Declined,
            InvitationStatus.Revoked => InvitationAcceptOutcome.Revoked,
            InvitationStatus.Expired => InvitationAcceptOutcome.Expired,
            _ => InvitationAcceptOutcome.NotFound,
        };

        return new AcceptInvitationResult(outcome);
    }

    private async Task<InvitationActionResult> ClassifyActionAsync(
        Guid invitationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invitation = await dbContext.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);

        if (invitation is null)
        {
            return new InvitationActionResult(InvitationActionOutcome.NotFound);
        }

        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now)
        {
            await ExpireAsync(invitationId, now, cancellationToken);
            return new InvitationActionResult(InvitationActionOutcome.Expired);
        }

        var outcome = invitation.Status switch
        {
            InvitationStatus.Accepted => InvitationActionOutcome.AlreadyUsed,
            InvitationStatus.Declined => InvitationActionOutcome.Declined,
            InvitationStatus.Revoked => InvitationActionOutcome.Revoked,
            InvitationStatus.Expired => InvitationActionOutcome.Expired,
            _ => InvitationActionOutcome.NotFound,
        };

        return new InvitationActionResult(outcome);
    }

    private Task<int> ExpireAsync(
        Guid invitationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return dbContext.Invitations
            .Where(candidate => candidate.Id == invitationId && candidate.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Status, InvitationStatus.Expired)
                    .SetProperty(candidate => candidate.DecidedAt, (DateTimeOffset?)now),
                cancellationToken);
    }

    private Task<Invitation?> FindByTokenAsync(
        byte[] tokenHash,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return dbContext.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invitation =>
                    invitation.TokenHash == tokenHash &&
                    invitation.NormalizedEmail == normalizedEmail,
                cancellationToken);
    }

    private Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        return dbContext.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invitation =>
                    invitation.Id == invitationId &&
                    invitation.NormalizedEmail == normalizedEmail,
                cancellationToken);
    }

    private Task<TripMemberRole?> ViewerRoleAsync(
        Guid tripId,
        UserId viewer,
        CancellationToken cancellationToken)
    {
        return dbContext.TripMembers
            .Where(member => member.TripId == tripId && member.UserId == viewer.Value)
            .Select(member => (TripMemberRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<string?> CallerEmailAsync(
        UserId viewer,
        CancellationToken cancellationToken)
    {
        return dbContext.Users
            .Where(account => account.Id == viewer.Value)
            .Select(account => account.NormalizedEmail)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> AccountEmailAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var email = await dbContext.Users
            .Where(account => account.Id == userId)
            .Select(account => account.Email ?? account.UserName)
            .FirstOrDefaultAsync(cancellationToken);

        return email ?? string.Empty;
    }

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqlException sql &&
        DuplicateKeyNumbers.Contains(sql.Number);
}
