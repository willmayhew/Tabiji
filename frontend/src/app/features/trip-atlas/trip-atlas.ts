import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith, Subject, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/http-error';
import { PlacesService } from '../../core/api/places.service';
import { TripsService } from '../../core/api/trips.service';
import { PlaceSelection, SavedPlace, SavedPlaceDraft } from '../../core/models/place';
import { Trip, TripDestination, TripStatus, UpdateTrip } from '../../core/models/trip';
import { formatTripDates } from '../../shared/format/trip-dates';
import { LocationPicker } from '../../shared/map/location-picker';
import { AtlasSelection } from '../../shared/map/map-engine';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';
import { PlaceEditor } from './place-editor';
import { TripAtlasList } from './trip-atlas-list';
import { TripAtlasMap } from './trip-atlas-map';
import { TripAtlasPanel } from './trip-atlas-panel';

interface AtlasResult {
  trip: Trip | null;
  error: string | null;
  notFound: boolean;
}

/** Puts a saved place into the list, in the order the server keeps it. */
function mergePlace(places: readonly SavedPlace[], saved: SavedPlace): SavedPlace[] {
  return places.some((place) => place.id === saved.id)
    ? places.map((place) => (place.id === saved.id ? saved : place))
    : [...places, saved];
}

/**
 * The trip as an atlas: the route on a map, the same route as a list, the
 * trip's saved places beside it, and one selection all three share.
 *
 * The screen owns the state and every request that changes it. The map, the
 * list and the panel are given what to draw and say what was chosen; none of
 * them holds a copy of the trip.
 *
 * The map is never required. Every stop and every place is readable in the
 * list, a map that cannot be drawn leaves the list and the editor working,
 * and a search that fails while saving a place leaves the trip untouched.
 */
@Component({
  selector: 'app-trip-atlas',
  imports: [
    RouterLink,
    StatusBadge,
    LocationPicker,
    PlaceEditor,
    TripAtlasMap,
    TripAtlasList,
    TripAtlasPanel,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas.html',
  styleUrl: './trip-atlas.css',
})
export class TripAtlas {
  private readonly tripsService = inject(TripsService);
  private readonly placesService = inject(PlacesService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  /** Emits when the reader asks for the trip again. */
  private readonly retry = new Subject<void>();
  private readonly routeId = signal('');

  readonly trip = signal<Trip | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notFound = signal(false);

  /** The places saved to the trip, kept apart from the trip's own failure. */
  readonly places = signal<SavedPlace[]>([]);
  readonly placesLoading = signal(true);
  readonly placesError = signal<string | null>(null);

  /** What went wrong while adding, changing or removing a place. */
  readonly placesActionError = signal<string | null>(null);

  /** The stop or place the map and the list agree is selected. */
  readonly selection = signal<AtlasSelection | null>(null);

  /** Which half of the atlas a narrow screen is showing. */
  protected readonly mode = signal<'map' | 'list'>('map');

  /** What the reader is told when the selection moves. */
  protected readonly announcement = signal<string | null>(null);

  /** The stop whose location is being changed, if any. */
  readonly pickerFor = signal<TripDestination | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  /** The place editor: open, and the place it is changing. */
  readonly editorOpen = signal(false);
  readonly editorPlace = signal<SavedPlace | null>(null);
  readonly editorSaving = signal(false);
  protected readonly editorError = signal<string | null>(null);

  private readonly atlasMap = viewChild(TripAtlasMap);

  constructor() {
    this.route.paramMap
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.trip.set(null);
          this.error.set(null);
          this.notFound.set(false);
          this.selection.set(null);
          this.saveError.set(null);
          this.pickerFor.set(null);
          this.places.set([]);
          this.placesLoading.set(true);
          this.placesError.set(null);
          this.placesActionError.set(null);
          this.editorOpen.set(false);
          this.editorPlace.set(null);
        }),
        map((params) => params.get('id') ?? ''),
        tap((id) => this.routeId.set(id)),
        switchMap((id) =>
          this.retry.pipe(
            startWith(undefined),
            switchMap(() =>
              this.tripsService.getById(id).pipe(
                map((trip): AtlasResult => ({ trip, error: null, notFound: false })),
                catchError((failure: HttpErrorResponse) =>
                  of<AtlasResult>({
                    trip: null,
                    error: describeHttpError(failure),
                    notFound: failure.status === 404,
                  }),
                ),
              ),
            ),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.trip.set(result.trip);
        this.error.set(result.error);
        this.notFound.set(result.notFound);
        this.loading.set(false);

        if (result.trip !== null) {
          this.loadPlaces(result.trip.id);
        } else {
          this.placesLoading.set(false);
        }
      });
  }

  /** The trip's id, so the way back works even before the trip has loaded. */
  protected readonly tripId = computed(() => this.trip()?.id ?? this.routeId());

  protected readonly dates = computed(() => {
    const trip = this.trip();

    return trip === null ? null : formatTripDates(trip.startsOn, trip.endsOn);
  });

  protected readonly ordered = computed(() => {
    const trip = this.trip();

    return trip === null
      ? []
      : [...trip.destinations].sort((left, right) => left.sortOrder - right.sortOrder);
  });

  /** How many stops can be drawn, so the map's empty state is honest. */
  protected readonly locatedCount = computed(
    () =>
      this.ordered().filter(
        (destination) => destination.latitude !== null && destination.longitude !== null,
      ).length,
  );

  /**
   * Whether there is anything at all to draw. A trip whose stops are all typed
   * by hand may still have places worth seeing, so the map is kept when it
   * has either.
   */
  protected readonly showsMap = computed(() => this.locatedCount() > 0 || this.places().length > 0);

  protected readonly selectedDestination = computed(() => {
    const selection = this.selection();

    return selection === null || selection.kind !== 'destination'
      ? null
      : (this.ordered().find((destination) => destination.id === selection.id) ?? null);
  });

  protected readonly selectedPlace = computed(() => {
    const selection = this.selection();

    return selection === null || selection.kind !== 'place'
      ? null
      : (this.places().find((place) => place.id === selection.id) ?? null);
  });

  /** The selected stop's place on the route, counting from one. */
  protected readonly selectedPosition = computed(() => {
    const selection = this.selection();

    if (selection === null || selection.kind !== 'destination') {
      return null;
    }

    const index = this.ordered().findIndex((destination) => destination.id === selection.id);

    return index < 0 ? null : index + 1;
  });

  protected readonly pickerQuery = computed(() => this.pickerFor()?.name ?? '');

  protected readonly pickerCoordinates = computed<PlaceSelection | null>(() => {
    const stop = this.pickerFor();

    if (stop === null || stop.latitude === null || stop.longitude === null) {
      return null;
    }

    return { name: stop.name, latitude: stop.latitude, longitude: stop.longitude };
  });

  protected retryLoad(): void {
    this.loading.set(true);
    this.error.set(null);
    this.retry.next();
  }

  protected retryPlaces(): void {
    const trip = this.trip();

    if (trip !== null) {
      this.loadPlaces(trip.id);
    }
  }

  protected setMode(mode: 'map' | 'list'): void {
    this.mode.set(mode);
  }

  protected frame(): void {
    this.atlasMap()?.frameTrip();
  }

  /** Selects a stop or a place, from the map or from the list. */
  protected select(selection: AtlasSelection): void {
    this.selection.set(selection);

    const name =
      selection.kind === 'destination'
        ? this.ordered().find((destination) => destination.id === selection.id)?.name
        : this.places().find((place) => place.id === selection.id)?.name;

    this.announcement.set(name === undefined ? null : `${name} selected.`);
  }

  /** Opens the picker for a stop's location. Nothing is changed until it is confirmed. */
  protected relocate(stop: TripDestination): void {
    this.saveError.set(null);
    this.pickerFor.set(stop);
  }

  protected closePicker(): void {
    this.pickerFor.set(null);
  }

  /** Opens the editor on an empty draft. */
  protected addPlace(): void {
    this.editorPlace.set(null);
    this.editorError.set(null);
    this.editorOpen.set(true);
  }

  /** Opens the editor on a saved place. */
  protected editPlace(place: SavedPlace): void {
    this.editorPlace.set(place);
    this.editorError.set(null);
    this.editorOpen.set(true);
  }

  /** Backs out of an edit without writing anything. */
  protected closeEditor(): void {
    this.editorOpen.set(false);
    this.editorPlace.set(null);
    this.editorError.set(null);
  }

  /**
   * Writes the draft the editor confirmed.
   *
   * The list and the map only change once the API has accepted the place, so
   * a refused save leaves no phantom marker behind and the editor stays open
   * with the reason.
   */
  protected savePlace(draft: SavedPlaceDraft): void {
    const trip = this.trip();

    if (trip === null || this.editorSaving()) {
      return;
    }

    const existing = this.editorPlace();

    this.editorSaving.set(true);
    this.editorError.set(null);
    this.placesActionError.set(null);

    const request$ =
      existing === null
        ? this.placesService.create(trip.id, draft)
        : this.placesService.update(trip.id, existing.id, draft);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (saved) => {
        this.editorSaving.set(false);
        this.editorOpen.set(false);
        this.editorPlace.set(null);

        this.places.update((places) => mergePlace(places, saved));
        this.selection.set({ kind: 'place', id: saved.id });
        this.announcement.set(`${saved.name} was saved.`);
      },
      error: (failure: HttpErrorResponse) => {
        this.editorSaving.set(false);
        this.editorError.set(describeHttpError(failure));
      },
    });
  }

  /**
   * Removes a place, once the reader has agreed to it.
   *
   * The place stays where it is until the API confirms, so a failed delete
   * leaves it on the trip with the reason beside it.
   */
  protected deletePlace(place: SavedPlace): void {
    const trip = this.trip();

    if (trip === null) {
      return;
    }

    if (!window.confirm(`Remove “${place.name}” from this trip?`)) {
      return;
    }

    this.placesActionError.set(null);

    this.placesService
      .remove(trip.id, place.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.places.update((places) => places.filter((candidate) => candidate.id !== place.id));

          const selection = this.selection();

          if (selection !== null && selection.kind === 'place' && selection.id === place.id) {
            this.selection.set(null);
          }

          this.announcement.set(`${place.name} was removed.`);
        },
        error: (failure: HttpErrorResponse) => {
          this.placesActionError.set(describeHttpError(failure));
        },
      });
  }

  /**
   * Takes the place the reader confirmed and gives it to the stop the picker
   * was opened for, and no other.
   *
   * The trip is only replaced in the page once the API has accepted it. A
   * refused change leaves the trip, and therefore the map, exactly as it was,
   * so a location that was not saved is never shown as though it were.
   */
  protected onPlaceChosen(place: PlaceSelection): void {
    const trip = this.trip();
    const target = this.pickerFor();

    this.pickerFor.set(null);

    if (trip === null || target === null) {
      return;
    }

    const destinations = trip.destinations.map((destination) =>
      destination.id === target.id
        ? { ...destination, name: place.name, latitude: place.latitude, longitude: place.longitude }
        : destination,
    );

    this.saving.set(true);
    this.saveError.set(null);

    this.tripsService
      .update(trip.id, this.toUpdateRequest(trip, destinations))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.trip.set(updated);
          this.announcement.set(`${place.name} was saved.`);
        },
        error: (failure: HttpErrorResponse) => {
          this.saving.set(false);
          this.saveError.set(describeHttpError(failure));
        },
      });
  }

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

  /**
   * Loads the trip's places on their own, so a failure here says so in the
   * places section without taking the trip, the route or the map with it.
   */
  private loadPlaces(tripId: string): void {
    this.placesLoading.set(true);
    this.placesError.set(null);

    this.placesService
      .listForTrip(tripId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (places) => {
          // A late answer for a trip the reader has already left must not
          // overwrite the one on screen.
          if (this.trip()?.id !== tripId) {
            return;
          }

          this.places.set(places);
          this.placesLoading.set(false);
        },
        error: (failure: HttpErrorResponse) => {
          if (this.trip()?.id !== tripId) {
            return;
          }

          this.placesError.set(describeHttpError(failure));
          this.placesLoading.set(false);
        },
      });
  }

  private toUpdateRequest(trip: Trip, destinations: readonly TripDestination[]): UpdateTrip {
    return {
      name: trip.name,
      startsOn: trip.startsOn,
      endsOn: trip.endsOn,
      destinations: destinations.map((destination) => ({
        id: destination.id,
        name: destination.name,
        startsOn: destination.startsOn,
        endsOn: destination.endsOn,
        latitude: destination.latitude,
        longitude: destination.longitude,
      })),
    };
  }
}
