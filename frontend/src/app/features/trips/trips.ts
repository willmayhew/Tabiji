import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { TripsService } from '../../core/api/trips.service';
import { Trip } from '../../core/models/trip';

@Component({
  selector: 'app-trips',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trips.html',
  styleUrl: './trips.css',
})
export class Trips {
  private readonly tripsService = inject(TripsService);

  protected name = '';
  protected destination = '';
  protected startsOn = '';
  protected endsOn = '';
  protected tripId = '';

  protected readonly created = signal<Trip | null>(null);
  protected readonly found = signal<Trip | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected createTrip(): void {
    this.busy.set(true);
    this.error.set(null);
    this.created.set(null);

    this.tripsService
      .create({
        name: this.name,
        destination: this.destination,
        startsOn: this.startsOn,
        endsOn: this.endsOn,
      })
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (trip) => {
          this.created.set(trip);
          this.tripId = trip.id;
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.error.set(this.describe(failure));
          this.busy.set(false);
        },
      });
  }

  protected findTrip(): void {
    this.busy.set(true);
    this.error.set(null);
    this.found.set(null);

    this.tripsService
      .getById(this.tripId.trim())
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (trip) => {
          this.found.set(trip);
          this.busy.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          this.error.set(this.describe(failure));
          this.busy.set(false);
        },
      });
  }

  private describe(failure: HttpErrorResponse): string {
    const detail = failure.error?.detail;

    return typeof detail === 'string' ? detail : `The API returned ${failure.status}.`;
  }
}
