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
import {
  stopsOf,
  Trip,
  TripDestination,
  TripDestinationUpdateDraft,
  TripStatus,
  TripStop,
  TripStopUpdateDraft,
  unfiledPlacesOf,
  UpdateTrip,
} from '../../core/models/trip';
import { formatTripDates } from '../../shared/format/trip-dates';
import { stopLabel } from '../../shared/format/place-labels';
import { AtlasSelection } from '../../shared/map/map-engine';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';
import { DestinationEditor, NewPlaceDraft } from './destination-editor';
import { StopDraft, StopEditor } from './stop-editor';
import { TripAtlasList } from './trip-atlas-list';
import { TripAtlasMap } from './trip-atlas-map';
import { TripAtlasPanel } from './trip-atlas-panel';

interface AtlasResult {
  trip: Trip | null;
  error: string | null;
  notFound: boolean;
}

/**
 * The trip as an atlas: the route on a map, the same route as a list, and one
 * selection all three share.
 *
 * The overview is the main route: destinations only, joined by the route line.
 * Selecting a destination opens its stops; selecting a stop shows the stop's
 * details and the stop editor. A stop is a place within its destination, never
 * a peer of a destination and never given a main-route number.
 *
 * The screen owns the state and every request that changes it. The map, the
 * list and the panel are given what to draw and say what was chosen; none of
 * them holds a copy of the trip. Every write is the whole trip, as it always
 * has been, so a refused change leaves the trip, the map and the list exactly
 * as they were.
 *
 * The map is never required. Every destination, stop and legacy place is
 * readable in the list, and a map that cannot be drawn leaves the list and the
 * editors working.
 */
@Component({
  selector: 'app-trip-atlas',
  imports: [
    RouterLink,
    StatusBadge,
    StopEditor,
    DestinationEditor,
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
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  /** Emits when the reader asks for the trip again. */
  private readonly retry = new Subject<void>();
  private readonly routeId = signal('');

  readonly trip = signal<Trip | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notFound = signal(false);

  /** The destination, stop or legacy place the map and the list agree is selected. */
  readonly selection = signal<AtlasSelection | null>(null);

  /** Which half of the atlas a narrow screen is showing. */
  protected readonly mode = signal<'map' | 'list'>('map');

  /** What the reader is told when the selection moves. */
  protected readonly announcement = signal<string | null>(null);

  /** Which destinations have their stops open. The list and the map share it. */
  readonly expanded = signal<ReadonlySet<string>>(new Set());

  /** Adding a parent destination. */
  readonly destinationEditorOpen = signal(false);
  readonly destinationEditorSaving = signal(false);
  protected readonly destinationEditorError = signal<string | null>(null);

  /** Adding or editing a stop. */
  readonly stopEditorOpen = signal(false);
  readonly stopEditorParent = signal<TripDestination | null>(null);
  readonly stopEditorStop = signal<TripStop | null>(null);
  readonly stopEditorSaving = signal(false);
  protected readonly stopEditorError = signal<string | null>(null);

  /** What went wrong while removing or filing a place. */
  protected readonly tripActionError = signal<string | null>(null);

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
          this.expanded.set(new Set());
          this.destinationEditorOpen.set(false);
          this.destinationEditorError.set(null);
          this.stopEditorOpen.set(false);
          this.stopEditorParent.set(null);
          this.stopEditorStop.set(null);
          this.stopEditorError.set(null);
          this.tripActionError.set(null);
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
          this.pruneExpanded(result.trip);
        }
      });
  }

  /** The trip's id, so the way back works even before the trip has loaded. */
  protected readonly tripId = computed(() => this.trip()?.id ?? this.routeId());

  protected readonly dates = computed(() => {
    const trip = this.trip();

    return trip === null ? null : formatTripDates(trip.startsOn, trip.endsOn);
  });

  protected readonly startsOn = computed(() => this.trip()?.startsOn ?? '');

  protected readonly endsOn = computed(() => this.trip()?.endsOn ?? '');

  /** The destinations on the main route, in order. */
  protected readonly ordered = computed(() => {
    const trip = this.trip();

    return trip === null
      ? []
      : [...trip.destinations].sort((left, right) => left.sortOrder - right.sortOrder);
  });

  /** The legacy saved places that are not yet in a destination. */
  protected readonly unfiled = computed(() => {
    const trip = this.trip();

    return trip === null ? [] : unfiledPlacesOf(trip);
  });

  /** How many destinations can be drawn, so the map's empty state is honest. */
  protected readonly locatedCount = computed(
    () =>
      this.ordered().filter(
        (destination) => destination.latitude !== null && destination.longitude !== null,
      ).length,
  );

  protected readonly selectedDestination = computed(() => {
    const selection = this.selection();

    return selection === null || selection.kind !== 'destination'
      ? null
      : (this.ordered().find((destination) => destination.id === selection.id) ?? null);
  });

  protected readonly selectedStop = computed<TripStop | null>(() => {
    const selection = this.selection();

    if (selection === null || selection.kind !== 'stop') {
      return null;
    }

    return this.findStop(selection.id);
  });

  protected readonly selectedPlace = computed<TripStop | null>(() => {
    const selection = this.selection();

    return selection === null || selection.kind !== 'place'
      ? null
      : (this.unfiled().find((place) => place.id === selection.id) ?? null);
  });

  /**
   * The destination whose journey the selection belongs to: the destination
   * itself, or the destination holding the selected stop. A legacy unfiled
   * place has no destination.
   */
  protected readonly selectedParent = computed<TripDestination | null>(() => {
    const selection = this.selection();

    if (selection === null) {
      return null;
    }

    if (selection.kind === 'destination') {
      return this.selectedDestination();
    }

    return selection.kind === 'stop' ? this.parentOfStop(selection.id) : null;
  });

  /** The selected destination's place on the main route, counting from one. */
  protected readonly selectedPosition = computed(() => {
    const parent = this.selectedParent();

    if (parent === null) {
      return null;
    }

    const index = this.ordered().findIndex((destination) => destination.id === parent.id);

    return index < 0 ? null : index + 1;
  });

  /** The hierarchical label of the selected stop, such as `1a`. */
  protected readonly selectedStopLabel = computed(() => {
    const parent = this.selectedParent();
    const stop = this.selectedStop();

    if (parent === null || stop === null) {
      return null;
    }

    const parentIndex = this.ordered().findIndex((destination) => destination.id === parent.id);
    const stopIndex = stopsOf(parent).findIndex((candidate) => candidate.id === stop.id);

    return parentIndex < 0 || stopIndex < 0 ? null : stopLabel(parentIndex, stopIndex);
  });

  /**
   * The stops of the selected destination, revealed only when it is expanded.
   * Collapsing a destination hides its stops from the map and the stop line
   * without touching the trip.
   */
  protected readonly visibleStops = computed<TripStop[]>(() => {
    const parent = this.selectedParent();

    if (parent === null || !this.expanded().has(parent.id)) {
      return [];
    }

    return stopsOf(parent);
  });

  /**
   * Whether there is anything at all to draw. The overview is the main route;
   * a selected destination's stops are drawn even when no destination has a
   * location, so a stop is never unreachable.
   */
  protected readonly showsMap = computed(
    () =>
      this.locatedCount() > 0 ||
      this.visibleStops().some((stop) => stop.latitude !== null && stop.longitude !== null),
  );

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

  /**
   * Selects a destination, stop or legacy place, from the map or the list.
   *
   * Choosing what is already selected returns to the overview, so a second
   * press on the same label is the way back.
   */
  protected select(selection: AtlasSelection): void {
    const current = this.selection();

    if (
      current !== null &&
      current.kind === selection.kind &&
      current.id === selection.id
    ) {
      this.clearSelection();
      return;
    }

    this.selection.set(selection);

    if (selection.kind === 'destination') {
      this.expand(selection.id);
    } else if (selection.kind === 'stop') {
      const parent = this.parentOfStop(selection.id);

      if (parent !== null) {
        this.expand(parent.id);
      }
    }

    const name =
      selection.kind === 'destination'
        ? this.ordered().find((destination) => destination.id === selection.id)?.name
        : selection.kind === 'stop'
          ? this.findStop(selection.id)?.name
          : this.unfiled().find((place) => place.id === selection.id)?.name;

    this.announcement.set(name === undefined ? null : `${name} selected.`);
  }

  /** Returns to the overview: the main route, and nothing else. */
  protected clearSelection(): void {
    this.selection.set(null);
    this.announcement.set('Overview.');
  }

  /** Opens or closes one destination's stops, independently of the others. */
  protected toggleDestination(destinationId: string): void {
    const open = new Set(this.expanded());
    const destination = this.ordered().find((candidate) => candidate.id === destinationId);
    const closing = open.has(destinationId);

    if (closing) {
      open.delete(destinationId);
    } else {
      open.add(destinationId);
    }

    this.expanded.set(open);

    // A stop cannot stay selected while the stops it belongs to are hidden:
    // the map would otherwise show one thing and the list another.
    const selected = this.selectedStop();

    if (closing && selected !== null && this.parentOfStop(selected.id)?.id === destinationId) {
      this.selection.set({ kind: 'destination', id: destinationId });
    }

    this.announcement.set(
      `${destination?.name ?? 'Destination'} stops ${closing ? 'hidden' : 'shown'}.`,
    );
  }

  /** Opens the destination dialog on an empty draft. */
  protected addDestination(): void {
    this.destinationEditorError.set(null);
    this.destinationEditorOpen.set(true);
  }

  /** Opens the stop editor on a new stop of the exact destination. */
  protected addStop(parent: TripDestination): void {
    this.stopEditorParent.set(parent);
    this.stopEditorStop.set(null);
    this.stopEditorError.set(null);
    this.stopEditorOpen.set(true);
    this.expand(parent.id);
    this.selection.set({ kind: 'destination', id: parent.id });
  }

  /** Opens the stop editor on an existing stop. */
  protected editStop(stop: TripStop): void {
    const parent = this.parentOfStop(stop.id);

    if (parent === null) {
      return;
    }

    this.stopEditorParent.set(parent);
    this.stopEditorStop.set(stop);
    this.stopEditorError.set(null);
    this.stopEditorOpen.set(true);
  }

  protected closeStopEditor(): void {
    if (this.stopEditorSaving()) {
      return;
    }

    this.stopEditorOpen.set(false);
    this.stopEditorParent.set(null);
    this.stopEditorStop.set(null);
    this.stopEditorError.set(null);
  }

  protected closeDestinationEditor(): void {
    if (this.destinationEditorSaving()) {
      return;
    }

    this.destinationEditorOpen.set(false);
    this.destinationEditorError.set(null);
  }

  /**
   * Writes a new parent destination.
   *
   * The whole trip is sent, as it is for every edit. The trip only changes
   * once the API has accepted it, so a refused addition leaves no phantom row
   * or marker.
   */
  protected saveDestination(draft: NewPlaceDraft): void {
    const trip = this.trip();

    if (trip === null || this.destinationEditorSaving()) {
      return;
    }

    const destinations = this.toDestinationDrafts(trip.destinations);

    destinations.push({
      id: null,
      name: draft.name,
      startsOn: draft.startsOn,
      endsOn: draft.endsOn,
      latitude: draft.latitude,
      longitude: draft.longitude,
      stops: [],
      category: null,
      address: null,
      notes: null,
      plannedOn: null,
    });

    this.destinationEditorSaving.set(true);
    this.destinationEditorError.set(null);
    this.tripActionError.set(null);

    this.tripsService
      .update(trip.id, this.toUpdateRequest(trip, destinations))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.destinationEditorSaving.set(false);
          this.destinationEditorOpen.set(false);
          this.trip.set(updated);

          const created = [...updated.destinations]
            .sort((left, right) => left.sortOrder - right.sortOrder)
            .at(-1);

          if (created !== undefined) {
            this.selection.set({ kind: 'destination', id: created.id });
            this.expand(created.id);
          }

          this.announcement.set(`${draft.name} was added.`);
        },
        error: (failure: HttpErrorResponse) => {
          this.destinationEditorSaving.set(false);
          this.destinationEditorError.set(describeHttpError(failure));
        },
      });
  }

  /**
   * Writes the stop the editor confirmed. The whole trip is sent; a refused
   * save leaves the trip and the editor exactly as they were.
   */
  protected saveStop(draft: StopDraft): void {
    const trip = this.trip();
    const parent = this.stopEditorParent();
    const editing = this.stopEditorStop();

    if (trip === null || parent === null || this.stopEditorSaving()) {
      return;
    }

    const destinations = this.toDestinationDrafts(trip.destinations);
    const target = destinations.find((destination) => destination.id === parent.id);

    if (target === undefined) {
      this.stopEditorError.set('That destination is no longer on the trip.');
      return;
    }

    const stopDraft: TripStopUpdateDraft = {
      id: editing?.id ?? null,
      name: draft.name,
      startsOn: draft.startsOn,
      endsOn: draft.endsOn,
      latitude: draft.latitude,
      longitude: draft.longitude,
      category: draft.category,
      address: draft.address,
      notes: draft.notes,
      plannedOn: draft.plannedOn,
    };

    if (editing === null) {
      target.stops.push(stopDraft);
    } else {
      const index = target.stops.findIndex((stop) => stop.id === editing.id);

      if (index < 0) {
        this.stopEditorError.set('That stop is no longer on the trip.');
        return;
      }

      target.stops[index] = stopDraft;
    }

    this.stopEditorSaving.set(true);
    this.stopEditorError.set(null);
    this.tripActionError.set(null);

    this.tripsService
      .update(trip.id, this.toUpdateRequest(trip, destinations))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.stopEditorSaving.set(false);
          this.stopEditorOpen.set(false);
          this.stopEditorParent.set(null);
          this.stopEditorStop.set(null);
          this.trip.set(updated);
          this.expand(parent.id);

          const holder = updated.destinations.find(
            (destination) => destination.id === parent.id,
          );
          const saved =
            editing === null
              ? holder === undefined
                ? undefined
                : [...stopsOf(holder)]
                    .sort((left, right) => left.sortOrder - right.sortOrder)
                    .at(-1)
              : stopsOf(holder ?? parent).find((stop) => stop.id === editing.id);

          if (saved !== undefined) {
            this.selection.set({ kind: 'stop', id: saved.id });
          }

          this.announcement.set(`${draft.name} was ${editing === null ? 'added' : 'saved'}.`);
        },
        error: (failure: HttpErrorResponse) => {
          this.stopEditorSaving.set(false);
          this.stopEditorError.set(describeHttpError(failure));
        },
      });
  }

  /**
   * Removes a stop once the reader agrees. The stop is the place: its category,
   * notes and planned day are removed with it. Nothing is deleted until the API
   * confirms, so a failed removal leaves the stop exactly where it was.
   */
  protected removeStop(stop: TripStop): void {
    const trip = this.trip();
    const parent = this.parentOfStop(stop.id);

    if (trip === null || parent === null) {
      return;
    }

    const agreed = window.confirm(
      `Remove “${stop.name}” from ${parent.name}? ` +
        'Its category, address, notes and planned day will be removed with it.',
    );

    if (!agreed) {
      return;
    }

    const destinations = this.toDestinationDrafts(trip.destinations);
    const target = destinations.find((destination) => destination.id === parent.id);

    if (target === undefined) {
      this.tripActionError.set('That destination is no longer on the trip.');
      return;
    }

    target.stops = target.stops.filter((candidate) => candidate.id !== stop.id);

    this.tripActionError.set(null);

    this.tripsService
      .update(trip.id, this.toUpdateRequest(trip, destinations))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.trip.set(updated);
          this.expand(parent.id);
          this.selection.set({ kind: 'destination', id: parent.id });
          this.announcement.set(`${stop.name} was removed.`);
        },
        error: (failure: HttpErrorResponse) => {
          this.tripActionError.set(describeHttpError(failure));
        },
      });
  }

  /**
   * Files one legacy unfiled place under a destination. It becomes a stop with
   * the same identity and all of its details; it is not duplicated.
   */
  protected filePlace(event: { place: TripStop; parentId: string }): void {
    const trip = this.trip();
    const parent = this.ordered().find((destination) => destination.id === event.parentId);

    if (trip === null || parent === undefined) {
      return;
    }

    const destinations = this.toDestinationDrafts(trip.destinations);
    const target = destinations.find((destination) => destination.id === parent.id);

    if (target === undefined) {
      this.tripActionError.set('That destination is no longer on the trip.');
      return;
    }

    target.stops.push({
      id: event.place.id,
      name: event.place.name,
      startsOn: event.place.startsOn,
      endsOn: event.place.endsOn,
      latitude: event.place.latitude,
      longitude: event.place.longitude,
      category: event.place.category ?? null,
      address: event.place.address ?? null,
      notes: event.place.notes ?? null,
      plannedOn: event.place.plannedOn ?? null,
    });

    const remaining = this.unfiled().filter((place) => place.id !== event.place.id);
    this.tripActionError.set(null);

    this.tripsService
      .update(trip.id, this.toUpdateRequest(trip, destinations, remaining))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.trip.set(updated);
          this.expand(parent.id);
          this.selection.set({ kind: 'stop', id: event.place.id });
          this.announcement.set(`${event.place.name} was filed under ${parent.name}.`);
        },
        error: (failure: HttpErrorResponse) => {
          this.tripActionError.set(describeHttpError(failure));
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

  private expand(destinationId: string): void {
    if (this.expanded().has(destinationId)) {
      return;
    }

    const next = new Set(this.expanded());
    next.add(destinationId);
    this.expanded.set(next);
  }

  /** Drops expansion state for destinations that are no longer on the trip. */
  private pruneExpanded(trip: Trip): void {
    const parents = new Set(trip.destinations.map((destination) => destination.id));
    const next = new Set([...this.expanded()].filter((id) => parents.has(id)));

    if (next.size !== this.expanded().size) {
      this.expanded.set(next);
    }
  }

  private findStop(id: string): TripStop | null {
    for (const destination of this.ordered()) {
      const stop = stopsOf(destination).find((candidate) => candidate.id === id);

      if (stop !== undefined) {
        return stop;
      }
    }

    return null;
  }

  private parentOfStop(id: string): TripDestination | null {
    return (
      this.ordered().find((destination) =>
        stopsOf(destination).some((stop) => stop.id === id),
      ) ?? null
    );
  }

  private toDestinationDrafts(
    destinations: readonly TripDestination[],
  ): TripDestinationUpdateDraft[] {
    return destinations.map((destination) => ({
      id: destination.id,
      name: destination.name,
      startsOn: destination.startsOn,
      endsOn: destination.endsOn,
      latitude: destination.latitude,
      longitude: destination.longitude,
      category: destination.category ?? null,
      address: destination.address ?? null,
      notes: destination.notes ?? null,
      plannedOn: destination.plannedOn ?? null,
      stops: stopsOf(destination).map(
        (stop): TripStopUpdateDraft => ({
          id: stop.id,
          name: stop.name,
          startsOn: stop.startsOn,
          endsOn: stop.endsOn,
          latitude: stop.latitude,
          longitude: stop.longitude,
          category: stop.category ?? null,
          address: stop.address ?? null,
          notes: stop.notes ?? null,
          plannedOn: stop.plannedOn ?? null,
        }),
      ),
    }));
  }

  private toUpdateRequest(
    trip: Trip,
    destinations: readonly TripDestinationUpdateDraft[],
    unfiledPlaces: readonly TripStop[] = unfiledPlacesOf(trip),
  ): UpdateTrip {
    return {
      name: trip.name,
      startsOn: trip.startsOn,
      endsOn: trip.endsOn,
      destinations: [...destinations],
      unfiledPlaces: unfiledPlaces.map((place) => ({
        id: place.id,
        name: place.name,
        startsOn: place.startsOn,
        endsOn: place.endsOn,
        latitude: place.latitude,
        longitude: place.longitude,
        category: place.category ?? null,
        address: place.address ?? null,
        notes: place.notes ?? null,
        plannedOn: place.plannedOn ?? null,
      })),
    };
  }
}
