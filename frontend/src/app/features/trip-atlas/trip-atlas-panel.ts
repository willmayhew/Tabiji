import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PlaceCategory } from '../../core/models/place';
import { stopsOf, TripDestination, TripStop } from '../../core/models/trip';
import { formatDestinationDates, formatPlanDay } from '../../shared/format/trip-dates';
import {
  placeCategoryLabel,
  stopLabel as labelStop,
} from '../../shared/format/place-labels';

/**
 * What the atlas says about whatever is selected: a destination on the main
 * route, a stop within one, or a legacy place not yet in a destination.
 *
 * The panel reads. A stop is edited and removed from here through the stop
 * editor; a destination's name, order and dates belong to the trip page; a
 * legacy place is filed under a destination from the Route list.
 */
@Component({
  selector: 'app-trip-atlas-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas-panel.html',
  styleUrl: './trip-atlas-panel.css',
})
export class TripAtlasPanel {
  /** The selected destination, or null when the selection is a stop or place. */
  readonly destination = input<TripDestination | null>(null);

  /** The selected stop, or null. */
  readonly stop = input<TripStop | null>(null);

  /** The destination holding the selected stop, when there is one. */
  readonly parent = input<TripDestination | null>(null);

  /** The selected legacy place not yet in a destination, or null. */
  readonly place = input<TripStop | null>(null);

  /** The selected destination's place on the main route, counting from one. */
  readonly position = input<number | null>(null);

  /** How many destinations the main route has. */
  readonly total = input(0);

  /** The trip's destinations, so a destination can list its stops. */
  readonly destinations = input<readonly TripDestination[]>([]);

  /** The hierarchical label of the selected stop, such as `1a`. */
  readonly stopLabel = input<string | null>(null);

  readonly editStop = output<TripStop>();

  readonly removeStop = output<TripStop>();

  /** Emits a stop of the selected destination, so the screen can select it. */
  readonly selectStop = output<TripStop>();

  protected readonly dates = computed(() => {
    const place = this.stop() ?? this.destination();

    return place === null
      ? null
      : (formatDestinationDates(place.startsOn, place.endsOn) ?? 'Dates not decided');
  });

  protected readonly located = computed(() => {
    const place = this.stop() ?? this.destination() ?? this.place();

    return place !== null && place.latitude !== null && place.longitude !== null;
  });

  protected readonly coordinates = computed(() => {
    const place = this.stop() ?? this.place();

    if (place === null || place.latitude === null || place.longitude === null) {
      return null;
    }

    return `${place.latitude.toFixed(4)}, ${place.longitude.toFixed(4)}`;
  });

  protected readonly plannedDay = computed(() => {
    const place = this.stop() ?? this.place();

    if (place === null || place.plannedOn == null) {
      return 'No day yet';
    }

    return formatPlanDay(place.plannedOn) ?? place.plannedOn;
  });

  protected readonly stopsAtDestination = computed(() => {
    const destination = this.destination();

    return destination === null ? [] : stopsOf(destination);
  });

  protected stopCountLabel(destination: TripDestination): string {
    const count = stopsOf(destination).length;

    return count === 1 ? '1 stop' : `${count} stops`;
  }

  protected isStopLocated(stop: TripStop): boolean {
    return stop.latitude !== null && stop.longitude !== null;
  }

  /** The hierarchical label of a stop in the selected destination. */
  protected labelFor(index: number): string {
    const position = this.position();

    return position === null ? '' : labelStop(position - 1, index);
  }

  protected categoryOf(place: TripStop | null): PlaceCategory | null {
    return place?.category ?? null;
  }

  protected categoryLabel(category: PlaceCategory): string {
    return placeCategoryLabel(category);
  }

  protected addressOf(place: TripStop | null): string | null {
    return place?.address ?? null;
  }

  protected notesOf(place: TripStop | null): string | null {
    return place?.notes ?? null;
  }
}
