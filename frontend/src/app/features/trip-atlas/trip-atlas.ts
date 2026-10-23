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
import { TripsService } from '../../core/api/trips.service';
import { PlaceSelection } from '../../core/models/place';
import { Trip, TripDestination, TripStatus, UpdateTrip } from '../../core/models/trip';
import { formatTripDates } from '../../shared/format/trip-dates';
import { LocationPicker } from '../../shared/map/location-picker';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';
import { TripAtlasList } from './trip-atlas-list';
import { TripAtlasMap } from './trip-atlas-map';
import { TripAtlasPanel } from './trip-atlas-panel';

interface AtlasResult {
  trip: Trip | null;
  error: string | null;
  notFound: boolean;
}

/**
 * The trip as an atlas: the whole route on a map, the same route as a list,
 * and one selection the two share.
 *
 * The screen owns the state. The map, the list and the panel are given what to
 * draw and say what was chosen; none of them holds a copy of the trip. The map
 * is never required: every stop is readable in the list, and a map that cannot
 * be drawn leaves the rest of the page untouched.
 */
@Component({
  selector: 'app-trip-atlas',
  imports: [RouterLink, StatusBadge, LocationPicker, TripAtlasMap, TripAtlasList, TripAtlasPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas.html',
  styleUrl: './trip-atlas.css',
})
export class TripAtlas {
  private readonly tripsService = inject(TripsService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  /** Emits when the reader asks for the map and the list again. */
  private readonly retry = new Subject<void>();
  private readonly routeId = signal('');

  readonly trip = signal<Trip | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notFound = signal(false);

  /** The stop the map and the list agree is selected. */
  readonly selectedId = signal<string | null>(null);

  /** Which half of the atlas a narrow screen is showing. */
  protected readonly mode = signal<'map' | 'list'>('map');

  /** What the reader is told when the selection moves. */
  protected readonly announcement = signal<string | null>(null);

  /** The stop whose location is being changed, if any. */
  readonly pickerFor = signal<TripDestination | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  private readonly atlasMap = viewChild(TripAtlasMap);

  constructor() {
    this.route.paramMap
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.trip.set(null);
          this.error.set(null);
          this.notFound.set(false);
          this.selectedId.set(null);
          this.saveError.set(null);
          this.pickerFor.set(null);
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

  protected readonly selected = computed(() => {
    const id = this.selectedId();

    return id === null
      ? null
      : (this.ordered().find((destination) => destination.id === id) ?? null);
  });

  /** The selected stop's place on the route, counting from one. */
  protected readonly selectedPosition = computed(() => {
    const id = this.selectedId();

    if (id === null) {
      return null;
    }

    const index = this.ordered().findIndex((destination) => destination.id === id);

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

  protected setMode(mode: 'map' | 'list'): void {
    this.mode.set(mode);
  }

  protected frame(): void {
    this.atlasMap()?.frameTrip();
  }

  /** Selects a stop, from either the map or the list. */
  protected select(id: string): void {
    const stop = this.ordered().find((destination) => destination.id === id);

    this.selectedId.set(id);
    this.announcement.set(stop === undefined ? null : `${stop.name} selected.`);
  }

  /** Opens the picker for a stop's location. Nothing is changed until it is confirmed. */
  protected relocate(stop: TripDestination): void {
    this.saveError.set(null);
    this.pickerFor.set(stop);
  }

  protected closePicker(): void {
    this.pickerFor.set(null);
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
