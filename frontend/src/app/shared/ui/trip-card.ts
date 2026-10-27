import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Trip, TripStatus } from '../../core/models/trip';
import { formatTripDates } from '../format/trip-dates';
import { routeLabel } from '../format/trip-destinations';
import { PlateAspect } from '../media/route-motif';
import { DestinationPlate } from './destination-plate';
import { BadgeTone, StatusBadge } from './status-badge';

/**
 * The summary of a trip: its plate, name, route, dates and status.
 * Used by the trips list, and reusable wherever a trip is listed.
 *
 * The plate carries the title of the trip, so that a card says which journey
 * it is at a glance, and the body sets the whole route out as a line.
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

  /** The title of the trip, which is what the plate is labelled with. */
  protected readonly title = computed(() => this.trip().name);

  /** The whole route, or a note when the trip has no stops at all. */
  protected readonly route = computed(() => {
    const label = routeLabel(this.trip().destinations);

    return label.length > 0 ? label : 'No destinations recorded';
  });

  /**
   * How much of the schedule has been decided, so that a partly planned trip
   * does not look as though it were fully scheduled.
   */
  protected readonly datedStops = computed(() => {
    const destinations = this.trip().destinations;

    if (destinations.length === 0) {
      return null;
    }

    const dated = destinations.filter(
      (destination) => destination.startsOn !== null || destination.endsOn !== null,
    ).length;

    return dated === 0 ? null : `Dates are set for ${dated} of ${destinations.length} destinations`;
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
