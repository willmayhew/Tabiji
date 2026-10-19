import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { Trip } from '../../core/models/trip';
import { TripCard } from '../../shared/ui/trip-card';

@Component({
  selector: 'app-trips',
  imports: [RouterLink, TripCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trips.html',
  styleUrl: './trips.css',
})
export class Trips {
  private readonly tripsService = inject(TripsService);
  private readonly destroyRef = inject(DestroyRef);

  readonly trips = signal<Trip[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  /** Placeholder cards shown while the list is loading. */
  protected readonly skeletonSlots = [0, 1, 2];

  constructor() {
    this.load();
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
          this.error.set(describeHttpError(failure));
          this.loading.set(false);
        },
      });
  }
}
