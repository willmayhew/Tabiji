import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TripDestination } from '../../core/models/trip';
import { formatDestinationDates } from '../../shared/format/trip-dates';

/**
 * What the atlas says about the stop that is selected.
 *
 * It is read-only apart from one action: changing where the stop is. Renaming,
 * reordering and the dates belong to the trip page's editor, so this does not
 * grow into a second one.
 */
@Component({
  selector: 'app-trip-atlas-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas-panel.html',
  styleUrl: './trip-atlas-panel.css',
})
export class TripAtlasPanel {
  /** The selected stop, or null when nothing is selected. */
  readonly destination = input<TripDestination | null>(null);

  /** The stop's place on the route, counting from one. */
  readonly position = input<number | null>(null);

  /** How many stops the route has. */
  readonly total = input(0);

  readonly changeLocation = output<TripDestination>();

  protected readonly dates = computed(() => {
    const stop = this.destination();

    return stop === null
      ? null
      : (formatDestinationDates(stop.startsOn, stop.endsOn) ?? 'Dates not decided');
  });

  protected readonly located = computed(() => {
    const stop = this.destination();

    return stop !== null && stop.latitude !== null && stop.longitude !== null;
  });
}
