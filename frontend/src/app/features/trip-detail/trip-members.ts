import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { describeHttpError } from '../../core/api/http-error';
import { InvitationsService } from '../../core/api/invitations.service';
import { TripMembersService } from '../../core/api/trip-members.service';
import { SessionService } from '../../core/auth/session.service';
import {
  CreatedInvitation,
  InvitationStatus,
  MemberRole,
  TripInvitation,
  TripMember,
  TripMembers as TripMembersView,
} from '../../core/models/member';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';

/**
 * The people on a trip, and the owner's ability to add, promote and remove
 * them.
 *
 * The panel owns its own requests, so the trip page keeps its single load.
 * The API is the boundary: an editor never sees these controls, and would be
 * refused if they called the endpoints directly.
 */
@Component({
  selector: 'app-trip-members',
  imports: [DatePipe, ReactiveFormsModule, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-members.html',
  styleUrl: './trip-members.css',
})
export class TripMembers implements OnInit {
  private readonly membersService = inject(TripMembersService);
  private readonly invitationsService = inject(InvitationsService);
  private readonly session = inject(SessionService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  readonly tripId = input.required<string>();

  readonly viewerRole = input.required<MemberRole>();

  /** Emits when the caller's own role changed, so the page can hide Delete. */
  readonly viewerRoleChanged = output<MemberRole>();

  /** Emits when the caller removed themselves from the trip. */
  readonly leftTrip = output<void>();

  readonly members = signal<TripMember[]>([]);
  readonly invitations = signal<TripInvitation[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly inviting = signal(false);
  readonly inviteSubmitted = signal(false);
  readonly inviteError = signal<string | null>(null);
  readonly inviteSuccess = signal<string | null>(null);
  readonly createdInvitation = signal<CreatedInvitation | null>(null);

  readonly actionError = signal<string | null>(null);
  readonly announcement = signal<string | null>(null);

  /** The member whose role change or removal is being written, so only that row goes quiet. */
  readonly busyMemberId = signal<string | null>(null);

  /** Why the last owner's controls are locked, in one place. */
  protected readonly lastOwnerHint = 'A trip must keep at least one owner.';

  readonly inviteForm = this.formBuilder.group({
    email: this.formBuilder.control('', {
      validators: [Validators.required, Validators.email],
    }),
  });

  /** Whether the development invitation link was just copied. */
  readonly linkCopied = signal(false);

  readonly ownerCount = computed(
    () => this.members().filter((member) => member.role === 'Owner').length,
  );

  readonly hasSingleOwner = computed(() => this.ownerCount() === 1);

  /** The invitations that could still be accepted, out of the whole history. */
  readonly pendingInvitations = computed(() =>
    this.invitations().filter((item) => item.status === 'Pending'),
  );

  ngOnInit(): void {
    this.load();
  }

  /** Reloads the members, and the invitations when the caller is an owner. */
  load(): void {
    this.loading.set(true);
    this.error.set(null);

    const tripId = this.tripId();
    const owner = this.viewerRole() === 'Owner';

    forkJoin({
      members: this.membersService.list(tripId),
      invitations: owner ? this.invitationsService.listForTrip(tripId) : of<TripInvitation[]>([]),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.applyMembers(result.members);
          this.invitations.set(result.invitations);
          this.loading.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.loading.set(false);

          if (failure.status === 401) {
            this.session.markSignedOut();
          }

          this.error.set(describeHttpError(failure));
        },
      });
  }

  protected isSelf(member: TripMember): boolean {
    return member.userId === this.session.user()?.id;
  }

  protected isLastOwner(member: TripMember): boolean {
    return member.role === 'Owner' && this.ownerCount() === 1;
  }

  protected roleId(member: TripMember): string {
    return `member-role-${member.userId}`;
  }

  protected roleTone(role: MemberRole): BadgeTone {
    return role === 'Owner' ? 'healthy' : 'neutral';
  }

  protected toneFor(status: InvitationStatus): BadgeTone {
    switch (status) {
      case 'Pending':
        return 'warning';
      case 'Accepted':
        return 'healthy';
      case 'Declined':
      case 'Revoked':
        return 'danger';
      default:
        return 'neutral';
    }
  }

  protected inviteEmailError(): string | null {
    const control = this.inviteForm.controls.email;

    if (!control.touched || !control.errors) {
      return null;
    }

    if (control.hasError('required')) {
      return 'An email address is required.';
    }

    if (control.hasError('email')) {
      return 'That does not look like an email address.';
    }

    return null;
  }

  protected invite(): void {
    this.inviteSubmitted.set(true);
    this.inviteError.set(null);
    this.inviteSuccess.set(null);
    this.createdInvitation.set(null);

    if (this.inviteForm.invalid) {
      this.inviteForm.markAllAsTouched();
      return;
    }

    const email = this.inviteForm.controls.email.value.trim();
    const tripId = this.tripId();

    this.inviting.set(true);

    this.invitationsService
      .create(tripId, email)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (created) => {
          this.inviting.set(false);
          this.inviteForm.reset();
          this.inviteSubmitted.set(false);
          this.linkCopied.set(false);
          this.createdInvitation.set(created);
          this.inviteSuccess.set(`Invitation sent to ${created.email}.`);
          this.invitations.update((list) => [created, ...list]);
          this.announcement.set(`Invited ${created.email}.`);
        },
        error: (failure: HttpErrorResponse) => {
          this.inviting.set(false);
          this.inviteError.set(describeHttpError(failure));
        },
      });
  }

  /**
   * Copies the development invitation link. The raw token is returned once,
   * when the invitation is created, so this is the only durable way to keep
   * it; the copy is announced as well as shown.
   */
  protected async copyLink(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.linkCopied.set(true);
      this.announcement.set('Invitation link copied.');
    } catch {
      this.actionError.set('The link could not be copied. Select it and copy it by hand.');
    }
  }

  protected changeRole(member: TripMember, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const role = select.value as MemberRole;

    if (role === member.role) {
      return;
    }

    if (
      member.role === 'Owner' &&
      role !== 'Owner' &&
      !window.confirm(`Make ${member.email} an editor? They will no longer manage this trip.`)
    ) {
      select.value = member.role;

      return;
    }

    this.actionError.set(null);
    this.busyMemberId.set(member.userId);

    this.membersService
      .changeRole(this.tripId(), member.userId, role)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (members) => {
          this.busyMemberId.set(null);
          this.applyMembers(members);
          this.announcement.set(
            `${member.email} is now ${role === 'Owner' ? 'an owner' : 'an editor'}.`,
          );
        },
        error: (failure: HttpErrorResponse) => {
          this.busyMemberId.set(null);
          this.actionError.set(describeHttpError(failure));
          select.value = member.role;
        },
      });
  }

  protected remove(member: TripMember): void {
    if (this.isLastOwner(member)) {
      this.actionError.set(this.lastOwnerHint);
      return;
    }

    if (!window.confirm(`Remove ${member.email} from this trip?`)) {
      return;
    }

    this.actionError.set(null);
    this.busyMemberId.set(member.userId);

    this.membersService
      .remove(this.tripId(), member.userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.busyMemberId.set(null);

          if (this.isSelf(member)) {
            this.leftTrip.emit();
            return;
          }

          this.announcement.set(`Removed ${member.email}.`);
          this.load();
        },
        error: (failure: HttpErrorResponse) => {
          this.busyMemberId.set(null);
          this.actionError.set(describeHttpError(failure));
        },
      });
  }

  protected revoke(invitation: TripInvitation): void {
    if (!window.confirm(`Revoke the invitation to ${invitation.email}?`)) {
      return;
    }

    this.actionError.set(null);

    this.invitationsService
      .revoke(this.tripId(), invitation.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.invitations.update((list) => list.filter((item) => item.id !== invitation.id));
          this.announcement.set(`Revoked the invitation to ${invitation.email}.`);
        },
        error: (failure: HttpErrorResponse) => {
          this.actionError.set(describeHttpError(failure));
        },
      });
  }

  /**
   * Takes the members the API returned. If the caller's own role changed —
   * an owner who demoted themselves — the page is told, so it can update the
   * controls it owns.
   */
  private applyMembers(view: TripMembersView): void {
    const previousRole = this.viewerRole();

    this.members.set(view.members);

    if (view.viewerRole !== previousRole) {
      this.viewerRoleChanged.emit(view.viewerRole);
    }
  }
}
