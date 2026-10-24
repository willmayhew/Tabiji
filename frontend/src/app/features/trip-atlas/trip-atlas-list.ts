import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { SavedPlace } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import { formatDestinationDates } from '../../shared/format/trip-dates';
import { routeSummary } from '../../shared/format/trip-destinations';
import { AtlasSelection } from '../../shared/map/map-engine';

/**
 * The trip as a list, and the accessible half of the atlas.
 *
 * It holds two different things. The route is the trip itself: ordered stops
 * that carry a place in the journey. The saved places are what is worth
 * seeing along the way — attractions, food, a hotel — and belong to no order
 * at all.
 *
 * Every stop and every place is a button, so selecting one is reachable from
 * the keyboard and everything on the map can be read here instead. The list
 * and the map share one selection; neither owns it.
 */
@Component({
  selector: 'app-trip-atlas-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas-list.html',
  styleUrl: './trip-atlas-list.css',
})
export class TripAtlasList {
  readonly destinations = input<readonly TripDestination[]>([]);

  readonly places = input<readonly SavedPlace[]>([]);

  /** The stop or place the map and the list agree is selected, if any. */
  readonly selection = input<AtlasSelection | null>(null);

  readonly placesLoading = input(false);

  readonly placesError = input<string | null>(null);

  readonly select = output<AtlasSelection>();

  readonly retryPlaces = output<void>();

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

  protected readonly placesSummary = computed(() => {
    const count = this.places().length;

    return count === 1 ? '1 place' : `${count} places`;
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

  protected isSelected(kind: AtlasSelection['kind'], id: string): boolean {
    const selection = this.selection();

    return selection !== null && selection.kind === kind && selection.id === id;
  }

  /**
   * The stop a place is filed under, or null when it is under none — or under
   * a stop that has since gone, which reads the same way rather than showing
   * a name the trip no longer has.
   */
  protected association(place: SavedPlace): string | null {
    if (place.destinationId === null) {
      return null;
    }

    return this.ordered().find((stop) => stop.id === place.destinationId)?.name ?? null;
  }
}
