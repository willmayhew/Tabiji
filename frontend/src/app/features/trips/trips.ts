import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { map, Observable } from 'rxjs';
import { describeHttpError } from '../../core/api/http-error';
import { InvitationsService } from '../../core/api/invitations.service';
import { TripsService } from '../../core/api/trips.service';
import { SessionService } from '../../core/auth/session.service';
import { AcceptedInvitation, RecipientInvitation } from '../../core/models/member';
import { Trip } from '../../core/models/trip';
import { SignInPrompt } from '../../shared/ui/sign-in-prompt';
import { StatusBadge } from '../../shared/ui/status-badge';
import { TripCard } from '../../shared/ui/trip-card';

@Component({
  selector: 'app-trips',
  imports: [DatePipe, RouterLink, TripCard, SignInPrompt, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trips.html',
  styleUrl: './trips.css',
})
export class Trips {
  private readonly tripsService = inject(TripsService);
  private readonly invitationsService = inject(InvitationsService);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly trips = signal<Trip[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  /** Invitations addressed to the signed-in account and still waiting. */
  readonly invitations = signal<RecipientInvitation[]>([]);
  readonly invitationError = signal<string | null>(null);
  readonly workingInvitation = signal<string | null>(null);

  /** Set when nobody is signed in, so the screen asks for nothing. */
  readonly signedOut = signal(false);

  /** Placeholder cards shown while the list is loading. */
  protected readonly skeletonSlots = [0, 1, 2];

  constructor() {
    // The session probe answers asynchronously. Until it does, the skeleton
    // stays up; afterwards, the list is either asked for or replaced by the
    // invitation to sign in. The API is still the boundary — this only avoids
    // a request the API would refuse.
    effect(() => {
      if (!this.session.isReady()) {
        return;
      }

      if (!this.session.isAuthenticated()) {
        this.trips.set([]);
        this.invitations.set([]);
        this.error.set(null);
        this.invitationError.set(null);
        this.loading.set(false);
        this.signedOut.set(true);
        return;
      }

      this.signedOut.set(false);
      this.load();
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.tripsService
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (trips) => {
          this.trips.set(trips);
          this.loading.set(false);
          this.loadInvitations();
        },
        error: (failure: HttpErrorResponse) => {
          this.loading.set(false);

          if (failure.status === 401) {
            this.session.markSignedOut();
            this.signedOut.set(true);
            return;
          }

          this.error.set(describeHttpError(failure));
        },
      });
  }

  protected acceptInvitation(invitation: RecipientInvitation): void {
    this.respond(invitation, () =>
      this.invitationsService.acceptById(invitation.id).pipe(map((accepted) => accepted)),
    );
  }

  protected declineInvitation(invitation: RecipientInvitation): void {
    this.respond(invitation, () =>
      this.invitationsService.declineById(invitation.id).pipe(map(() => null)),
    );
  }

  /** Loads the invitations waiting for this account, after the trips arrive. */
  private loadInvitations(): void {
    this.invitationError.set(null);

    this.invitationsService
      .mine()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (invitations) => this.invitations.set(invitations),
        error: (failure: HttpErrorResponse) => {
          if (failure.status === 401) {
            this.session.markSignedOut();
            this.signedOut.set(true);
            return;
          }

          this.invitationError.set(describeHttpError(failure));
        },
      });
  }

  private respond(
    invitation: RecipientInvitation,
    request: () => Observable<AcceptedInvitation | null>,
  ): void {
    if (this.workingInvitation() !== null) {
      return;
    }

    this.workingInvitation.set(invitation.id);
    this.invitationError.set(null);

    request()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (accepted) => {
          this.workingInvitation.set(null);
          this.invitations.update((list) => list.filter((item) => item.id !== invitation.id));

          if (accepted !== null) {
            void this.router.navigate(['/trips', accepted.tripId]);
          }
        },
        error: (failure: HttpErrorResponse) => {
          this.workingInvitation.set(null);

          if (failure.status === 401) {
            this.session.markSignedOut();
            this.signedOut.set(true);
            return;
          }

          this.invitationError.set(describeHttpError(failure));
        },
      });
  }
}
