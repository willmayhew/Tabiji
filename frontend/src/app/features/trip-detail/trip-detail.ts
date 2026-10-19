import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { Trip } from '../../core/models/trip';
import { TripCard } from '../../shared/ui/trip-card';

interface TripDetailResult {
  trip: Trip | null;
  error: string | null;
  notFound: boolean;
}

@Component({
  selector: 'app-trip-detail',
  imports: [RouterLink, TripCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-detail.html',
  styleUrl: './trip-detail.css',
})
export class TripDetail {
  private readonly tripsService = inject(TripsService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

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
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.trip.set(result.trip);
        this.error.set(result.error);
        this.notFound.set(result.notFound);
        this.loading.set(false);
      });
  }
}
