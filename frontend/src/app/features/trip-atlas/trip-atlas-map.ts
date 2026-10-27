import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  OnDestroy,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { environment } from '../../../environments/environment';
import { TripDestination, TripStop } from '../../core/models/trip';
import { parentLabel, stopLabel } from '../../shared/format/place-labels';
import {
  AtlasHandle,
  AtlasPoint,
  AtlasSelection,
  AtlasStopPoint,
  MapEngine,
} from '../../shared/map/map-engine';

/**
 * The trip drawn on a map: a numbered marker for every located destination,
 * a labelled marker for every visible stop, and two distinct lines — the main
 * route through the destinations, and the connection between the stops of the
 * selected destination.
 *
 * The component is presentational. It owns only the map instance; the
 * selection, the expansion state and the trip belong to the screen above it.
 * The map is never the only way to reach a stop — the list beside it carries
 * every one of them as text — so the markers are hidden from assistive
 * technology.
 */
@Component({
  selector: 'app-trip-atlas-map',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas-map.html',
  styleUrl: './trip-atlas-map.css',
})
export class TripAtlasMap implements OnDestroy {
  private readonly mapEngine = inject(MapEngine);

  /** The trip's destinations, in route order. Only the located ones are drawn. */
  readonly destinations = input<readonly TripDestination[]>([]);

  /** The stops of the selected destination, when it is expanded. */
  readonly stops = input<readonly TripStop[]>([]);

  /** The selected destination's main-route position, counting from one. */
  readonly parentPosition = input<number | null>(null);

  /** The stop the screen has selected, if any. */
  readonly selection = input<AtlasSelection | null>(null);

  /** The trip's name, for the map's accessible name. */
  readonly label = input('');

  /** Whether the map is on screen, so a map that returns can be re-measured. */
  readonly active = input(true);

  /** Emits the destination or stop whose marker was clicked. */
  readonly select = output<AtlasSelection>();

  protected readonly unavailable = signal(false);

  /** Whether something the map needed — its tiles, most likely — failed. */
  protected readonly degraded = signal(false);

  private readonly frame = viewChild<ElementRef<HTMLElement>>('canvas');

  private handle: AtlasHandle | null = null;
  private starting = false;

  /**
   * The destinations that can be drawn, labelled by their place on the whole
   * route. A destination with no location is skipped but still counts towards
   * the numbering, so a marker's label always matches its position.
   */
  protected readonly points = computed<AtlasPoint[]>(() => {
    const ordered = [...this.destinations()].sort(
      (left, right) => left.sortOrder - right.sortOrder,
    );

    const located: AtlasPoint[] = [];

    ordered.forEach((destination, position) => {
      if (destination.latitude !== null && destination.longitude !== null) {
        located.push({
          id: destination.id,
          latitude: destination.latitude,
          longitude: destination.longitude,
          label: parentLabel(position),
        });
      }
    });

    return located;
  });

  /** The visible stops, with the hierarchical labels of their parent's stops. */
  protected readonly stopPoints = computed<AtlasStopPoint[]>(() => {
    const position = this.parentPosition();

    if (position === null) {
      return [];
    }

    // The label comes from the stop's place in the whole list, before a stop
    // without coordinates is skipped: an unlocated stop still takes its
    // letter, so the map and the list never disagree about `2b`.
    return this.stops()
      .map((stop, index) => ({ stop, label: stopLabel(position - 1, index) }))
      .filter(
        ({ stop }) => stop.latitude !== null && stop.longitude !== null,
      )
      .map(({ stop, label }) => ({
        id: stop.id,
        latitude: stop.latitude as number,
        longitude: stop.longitude as number,
        label,
      }));
  });

  constructor() {
    afterNextRender(() => void this.start());

    // Each signal is read before the handle is used. Reading it inside the
    // optional call would let the chain short-circuit while the map is still
    // being built, and an effect that reads nothing never runs again.
    effect(() => {
      const points = this.points();
      this.handle?.setDestinations(points);
    });

    effect(() => {
      const points = this.stopPoints();
      this.handle?.setStops(points);
    });

    effect(() => {
      const points = this.points();
      this.handle?.setRoute(points);
    });

    effect(() => {
      const points = this.stopPoints();
      this.handle?.setStopRoute(points);
    });

    effect(() => {
      const selection = this.selection();
      this.handle?.setSelection(selection);
      this.handle?.reveal(selection);
    });

    // A map that was hidden has no size to measure with, so it is asked to
    // measure itself again the moment it returns to the screen.
    effect(() => {
      const active = this.active();

      if (active) {
        this.handle?.resize();
      }
    });
  }

  /** Frames every destination the map is drawing. */
  frameTrip(): void {
    this.handle?.fitToTrip();
  }

  private async start(): Promise<void> {
    const container = this.frame()?.nativeElement;

    if (container === undefined || this.starting) {
      return;
    }

    this.starting = true;

    let handle: AtlasHandle | null = null;

    try {
      handle = await this.mapEngine.createAtlas(container, { styleUrl: environment.map.styleUrl });
    } catch {
      handle = null;
    }

    this.starting = false;

    if (handle === null) {
      this.unavailable.set(true);
      return;
    }

    this.unavailable.set(false);
    this.handle = handle;

    handle.onSelect((selection) => this.select.emit(selection));
    handle.onError(() => this.degraded.set(true));

    // The inputs may have arrived while the map was being built.
    handle.setDestinations(this.points());
    handle.setStops(this.stopPoints());
    handle.setRoute(this.points());
    handle.setStopRoute(this.stopPoints());
    handle.setSelection(this.selection());
    handle.fitToTrip();
    handle.reveal(this.selection());
  }

  ngOnDestroy(): void {
    this.handle?.destroy();
    this.handle = null;
  }
}
