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
import { SavedPlace } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import {
  AtlasHandle,
  AtlasPoint,
  AtlasSelection,
  LocatedPoint,
  MapEngine,
} from '../../shared/map/map-engine';

/**
 * The trip drawn on a map: a numbered marker for every located destination, a
 * dot for every saved place, and one dashed line that joins the destinations
 * in the order they are visited.
 *
 * The component is presentational. It owns only the map instance; the
 * selection and the trip belong to the screen above it. The map is never the
 * only way to reach a stop or a place — the list beside it carries every one
 * of them as text — so the markers are hidden from assistive technology.
 */
@Component({
  selector: 'app-trip-atlas-map',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas-map.html',
  styleUrl: './trip-atlas-map.css',
})
export class TripAtlasMap implements OnDestroy {
  private readonly mapEngine = inject(MapEngine);

  /** The trip's whole route, in order. Only the located stops are drawn. */
  readonly destinations = input<readonly TripDestination[]>([]);

  /** The places saved to the trip. A place always has a position. */
  readonly places = input<readonly SavedPlace[]>([]);

  /** The stop or place the screen has selected, if any. */
  readonly selection = input<AtlasSelection | null>(null);

  /** The trip's name, for the map's accessible name. */
  readonly label = input('');

  /** Whether the map is on screen, so a map that returns can be re-measured. */
  readonly active = input(true);

  /** Emits the stop or place whose marker was clicked. */
  readonly select = output<AtlasSelection>();

  protected readonly unavailable = signal(false);

  /** Whether something the map needed — its tiles, most likely — failed. */
  protected readonly degraded = signal(false);

  private readonly frame = viewChild<ElementRef<HTMLElement>>('canvas');

  private handle: AtlasHandle | null = null;
  private starting = false;

  /**
   * The stops that can be drawn, numbered by their place on the whole route.
   * A stop with no location is skipped but still counts towards the numbering,
   * so a marker's number always matches the stop's position.
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
          index: position + 1,
        });
      }
    });

    return located;
  });

  /** The saved places, which always carry a position of their own. */
  protected readonly placePoints = computed<LocatedPoint[]>(() =>
    this.places().map((place) => ({
      id: place.id,
      latitude: place.latitude,
      longitude: place.longitude,
    })),
  );

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
      const points = this.placePoints();
      this.handle?.setPlaces(points);
    });

    effect(() => {
      const points = this.points();
      this.handle?.setRoute(points);
    });

    effect(() => {
      const selection = this.selection();
      this.handle?.setSelection(selection);
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
    handle.setPlaces(this.placePoints());
    handle.setRoute(this.points());
    handle.setSelection(this.selection());
    handle.fitToTrip();
  }

  ngOnDestroy(): void {
    this.handle?.destroy();
    this.handle = null;
  }
}
