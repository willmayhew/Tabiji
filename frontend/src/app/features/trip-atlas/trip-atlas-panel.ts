import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  viewChild,
} from '@angular/core';
import { SavedPlace } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import { formatDestinationDates } from '../../shared/format/trip-dates';

/**
 * What the atlas says about whatever is selected: a stop on the route, or a
 * place saved to the trip.
 *
 * A stop can only have its location changed here; renaming, reordering and
 * the dates belong to the trip page's editor, so this does not grow into a
 * second one. A saved place is the atlas's own, so it is edited and removed
 * from here.
 *
 * The stop also says what is filed under it. On a phone the list is hidden
 * while the map is showing, so this is the only place a stop's places appear.
 */
@Component({
  selector: 'app-trip-atlas-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas-panel.html',
  styleUrl: './trip-atlas-panel.css',
})
export class TripAtlasPanel {
  /** The selected stop, or null when the selection is a place or nothing. */
  readonly destination = input<TripDestination | null>(null);

  /** The selected saved place, or null. */
  readonly place = input<SavedPlace | null>(null);

  /** The stop's place on the route, counting from one. */
  readonly position = input<number | null>(null);

  /** How many stops the route has. */
  readonly total = input(0);

  /** The trip's stops, so a place can say which one it is filed under. */
  readonly destinations = input<readonly TripDestination[]>([]);

  /** The trip's saved places, so a stop can say what is filed under it. */
  readonly places = input<readonly SavedPlace[]>([]);

  readonly changeLocation = output<TripDestination>();

  readonly editPlace = output<SavedPlace>();

  readonly deletePlace = output<SavedPlace>();

  /** Emits a place filed under the selected stop, so the screen can select it. */
  readonly selectPlace = output<SavedPlace>();

  private readonly injector = inject(Injector);

  /** The name of the place, once the panel is showing one. */
  private readonly placeHeading = viewChild<ElementRef<HTMLElement>>('placeHeading');

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

  /**
   * Where the selected place is filed, in words. A place filed under a stop
   * that has since gone reads as unassigned, rather than naming a stop the
   * trip no longer has.
   */
  protected readonly filedUnder = computed(() => {
    const place = this.place();

    if (place === null || place.destinationId === null) {
      return 'Not on the route';
    }

    return this.destinations().find((stop) => stop.id === place.destinationId)?.name ?? 'Not on the route';
  });

  protected readonly coordinates = computed(() => {
    const place = this.place();

    return place === null ? null : `${place.latitude.toFixed(4)}, ${place.longitude.toFixed(4)}`;
  });

  /**
   * The places filed under the selected stop — the same rule the plan list
   * groups by, so the two can never disagree about where a place belongs.
   */
  protected readonly placesAtStop = computed(() => {
    const stop = this.destination();

    return stop === null ? [] : this.places().filter((place) => place.destinationId === stop.id);
  });

  protected readonly placesAtStopLabel = computed(() => {
    const count = this.placesAtStop().length;

    return count === 1 ? '1 place' : `${count} places`;
  });

  /**
   * Selects a place filed under the stop.
   *
   * The stop's own content — including the button the reader just pressed —
   * is replaced by that place, so the focus would be left with nothing. It is
   * moved to the place's name instead, so a keyboard reader carries on from
   * where they were rather than from the top of the page.
   */
  protected chooseFromStop(place: SavedPlace): void {
    this.selectPlace.emit(place);

    afterNextRender(() => this.placeHeading()?.nativeElement.focus(), {
      injector: this.injector,
    });
  }
}
