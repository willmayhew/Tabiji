import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TripDestination } from '../../core/models/trip';
import { formatDestinationDates } from '../../shared/format/trip-dates';
import { routeSummary } from '../../shared/format/trip-destinations';

/**
 * The route as a list, and the accessible half of the atlas.
 *
 * Every stop is a button, so selecting one is reachable from the keyboard and
 * every stop can be read without the map. The list and the map share one
 * selection; neither owns it.
 */
@Component({
  selector: 'app-trip-atlas-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas-list.html',
  styleUrl: './trip-atlas-list.css',
})
export class TripAtlasList {
  readonly destinations = input<readonly TripDestination[]>([]);

  /** The stop the map and the list agree is selected, if any. */
  readonly selectedId = input<string | null>(null);

  readonly select = output<string>();

  protected readonly ordered = computed(() =>
    [...this.destinations()].sort((left, right) => left.sortOrder - right.sortOrder),
  );

  protected readonly locatedCount = computed(
    () => this.ordered().filter((destination) => this.isLocated(destination)).length,
  );

  protected readonly summary = computed(() => routeSummary(this.ordered()));

  /**
   * How much of the route is on the map, or null when all of it is. A partly
   * located trip says so rather than looking complete.
   */
  protected readonly mapNote = computed(() => {
    const total = this.ordered().length;
    const located = this.locatedCount();

    if (total === 0 || located === total) {
      return null;
    }

    return located === 0 ? 'No stop is on the map yet' : `${located} of ${total} stops are on the map`;
  });

  protected isLocated(destination: TripDestination): boolean {
    return destination.latitude !== null && destination.longitude !== null;
  }

  /** The dates of a stop, or a note that they have not been decided. */
  protected datesFor(destination: TripDestination): string {
    return formatDestinationDates(destination.startsOn, destination.endsOn) ?? 'Dates not decided';
  }

  protected hasDates(destination: TripDestination): boolean {
    return destination.startsOn !== null || destination.endsOn !== null;
  }

  /** The number of a stop, counting from one, as it is set on the map. */
  protected position(index: number): string {
    const number = index + 1;

    return number < 10 ? `0${number}` : String(number);
  }
}
