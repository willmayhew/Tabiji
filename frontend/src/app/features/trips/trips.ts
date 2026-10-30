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
import { RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { SessionService } from '../../core/auth/session.service';
import { Trip } from '../../core/models/trip';
import { SignInPrompt } from '../../shared/ui/sign-in-prompt';
import { TripCard } from '../../shared/ui/trip-card';

@Component({
  selector: 'app-trips',
  imports: [RouterLink, TripCard, SignInPrompt],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trips.html',
  styleUrl: './trips.css',
})
export class Trips {
  private readonly tripsService = inject(TripsService);
  private readonly session = inject(SessionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly trips = signal<Trip[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

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
        this.error.set(null);
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
}
