import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Trip } from '../../core/models/trip';
import { TripForm } from '../../shared/form/trip-form';

/**
 * The page that holds the create trip form. It exists so the form can have
 * a page of its own, rather than sharing a column with the trips list.
 */
@Component({
  selector: 'app-trip-create',
  imports: [RouterLink, TripForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-create.html',
  styleUrl: './trip-create.css',
})
export class TripCreate {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private navigateTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    this.destroyRef.onDestroy(() => clearTimeout(this.navigateTimer));
  }

  protected onCreated(trip: Trip): void {
    // Leave the success message on screen long enough to be read before
    // moving on to the trip that was just created.
    this.navigateTimer = setTimeout(() => {
      void this.router.navigate(['/trips', trip.id]);
    }, 1200);
  }
}
