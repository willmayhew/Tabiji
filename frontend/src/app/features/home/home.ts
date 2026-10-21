import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { HealthService } from '../../core/api/health.service';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { HealthReport, HealthStatus } from '../../core/models/health-status';
import { Trip } from '../../core/models/trip';
import { formatTripDates } from '../../shared/format/trip-dates';
import { routeLabel } from '../../shared/format/trip-destinations';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';

@Component({
  selector: 'app-home',
  imports: [RouterLink, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  private readonly healthService = inject(HealthService);
  private readonly tripsService = inject(TripsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly liveness = signal<HealthReport | null>(null);
  protected readonly readiness = signal<HealthReport | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  /** The journeys in the workspace, for the index under the masthead. */
  protected readonly trips = signal<Trip[]>([]);
  protected readonly tripsLoading = signal(true);
  protected readonly tripsError = signal<string | null>(null);

  constructor() {
    this.refresh();
    this.loadTrips();
  }

  protected refresh(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      liveness: this.healthService.getLiveness().pipe(catchError(() => of(null))),
      readiness: this.healthService.getReadiness().pipe(catchError(() => of(null))),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ liveness, readiness }) => {
        this.liveness.set(liveness);
        this.readiness.set(readiness);
        this.error.set(
          liveness === null && readiness === null
            ? 'The Tabiji API is not reachable. Start it with "dotnet run --project backend/Tabiji.Api" and try again.'
            : null,
        );
        this.loading.set(false);
      });
  }

  /**
   * Loads the journeys shown in the index.
   *
   * The index is a separate concern from the health probes, so it has its
   * own state. A failure here never takes the status strip down with it.
   */
  protected loadTrips(): void {
    this.tripsLoading.set(true);
    this.tripsError.set(null);

    this.tripsService
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (trips) => {
          this.trips.set(trips.slice(0, 3));
          this.tripsLoading.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.tripsError.set(describeHttpError(failure));
          this.tripsLoading.set(false);
        },
      });
  }

  protected dateRange(trip: Trip): string | null {
    const range = formatTripDates(trip.startsOn, trip.endsOn);

    return range === null ? null : range.label;
  }

  /** The route of a journey, as one line. */
  protected route(trip: Trip): string {
    const label = routeLabel(trip.destinations);

    return label.length > 0 ? label : 'No destinations recorded';
  }

  protected toneFor(status: HealthStatus): BadgeTone {
    switch (status) {
      case 'Healthy':
        return 'healthy';
      case 'Degraded':
        return 'warning';
      default:
        return 'danger';
    }
  }
}
