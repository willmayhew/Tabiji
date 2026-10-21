import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith, Subject, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { Trip, TripStatus } from '../../core/models/trip';
import { formatTripDate, formatTripDates } from '../../shared/format/trip-dates';
import { firstDestination, routeSummary } from '../../shared/format/trip-destinations';
import { DestinationPlate } from '../../shared/ui/destination-plate';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';

interface TripDetailResult {
  trip: Trip | null;
  error: string | null;
  notFound: boolean;
}

@Component({
  selector: 'app-trip-detail',
  imports: [DestinationPlate, RouterLink, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-detail.html',
  styleUrl: './trip-detail.css',
})
export class TripDetail {
  private readonly tripsService = inject(TripsService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  /** Emits when the reader asks to try the request again. */
  private readonly retry = new Subject<void>();

  readonly trip = signal<Trip | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notFound = signal(false);

  constructor() {
    this.route.paramMap
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.trip.set(null);
          this.error.set(null);
          this.notFound.set(false);
        }),
        map((params) => params.get('id') ?? ''),
        switchMap((id) =>
          this.retry.pipe(
            startWith(undefined),
            switchMap(() =>
              this.tripsService.getById(id).pipe(
                map((trip): TripDetailResult => ({ trip, error: null, notFound: false })),
                catchError((failure: HttpErrorResponse) =>
                  of<TripDetailResult>({
                    trip: null,
                    error: describeHttpError(failure),
                    notFound: failure.status === 404,
                  }),
                ),
              ),
            ),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.trip.set(result.trip);
        this.error.set(result.error);
        this.notFound.set(result.notFound);
        this.loading.set(false);
      });
  }

  protected retryLoad(): void {
    this.loading.set(true);
    this.error.set(null);
    this.retry.next();
  }

  protected readonly dates = computed(() => {
    const found = this.trip();

    return found === null ? null : formatTripDates(found.startsOn, found.endsOn);
  });

  protected readonly startDate = computed(() => {
    const found = this.trip();

    return found === null ? null : formatTripDate(found.startsOn);
  });

  protected readonly endDate = computed(() => {
    const found = this.trip();

    return found === null ? null : formatTripDate(found.endsOn);
  });

  /** Where the journey starts, which is what the cover band is labelled with. */
  protected readonly origin = computed(() => {
    const found = this.trip();

    return found === null ? null : firstDestination(found.destinations);
  });

  /** How many stops the route has, in words. */
  protected readonly routeSummary = computed(() => {
    const found = this.trip();

    return found === null ? '' : routeSummary(found.destinations);
  });

  protected toneFor(status: TripStatus): BadgeTone {
    switch (status) {
      case 'Confirmed':
        return 'healthy';
      case 'Cancelled':
        return 'danger';
      default:
        return 'neutral';
    }
  }
}
