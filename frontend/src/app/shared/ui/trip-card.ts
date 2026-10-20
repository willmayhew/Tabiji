import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Trip, TripStatus } from '../../core/models/trip';
import { formatTripDates } from '../format/trip-dates';
import { PlateAspect } from '../media/route-motif';
import { DestinationPlate } from './destination-plate';
import { BadgeTone, StatusBadge } from './status-badge';

/**
 * The summary of a trip: its plate, name, destination, dates and status.
 * Used by the trips list, and reusable wherever a trip is listed.
 *
 * The plate is drawn from the trip's id, so every card in a grid carries its
 * own graphic and no two look the same.
 */
@Component({
  selector: 'app-trip-card',
  imports: [DestinationPlate, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-card.html',
  styleUrl: './trip-card.css',
})
export class TripCard {
  readonly trip = input.required<Trip>();

  /** The position of the trip in a list, shown as "No. 03" on the plate. */
  readonly index = input<number | null>(null);

  /** The shape of the plate. The leading trip in a grid uses the wide band. */
  readonly aspect = input<PlateAspect>('card');

  protected readonly dates = computed(() =>
    formatTripDates(this.trip().startsOn, this.trip().endsOn),
  );

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
