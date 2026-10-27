import { HttpErrorResponse } from '@angular/common/http';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { PlaceCategory } from '../../core/models/place';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { PlaceSelection } from '../../core/models/place';
import {
  CreateTrip,
  stopsOf,
  Trip,
  TripDestination,
  TRIP_DESTINATION_MAX_LENGTH,
  TRIP_MAX_DESTINATIONS,
  TRIP_MAX_STOPS_PER_DESTINATION,
  TRIP_NAME_MAX_LENGTH,
  UpdateTrip,
} from '../../core/models/trip';
import { LocationPicker } from '../map/location-picker';
import { tripDateRangeValidator } from '../validate/trip-date-range';
import {
  destinationDatesValidator,
  nonBlankValidator,
  tripDestinationsValidator,
  tripScheduleValidator,
  tripStopsValidator,
} from '../validate/trip-destinations';

type StopGroup = FormGroup<{
  id: FormControl<string | null>;
  name: FormControl<string>;
  startsOn: FormControl<string>;
  endsOn: FormControl<string>;
  latitude: FormControl<number | null>;
  longitude: FormControl<number | null>;
  category: FormControl<PlaceCategory | null>;
  address: FormControl<string>;
  notes: FormControl<string>;
  plannedOn: FormControl<string>;
}>;

type DestinationGroup = FormGroup<{
  id: FormControl<string | null>;
  name: FormControl<string>;
  startsOn: FormControl<string>;
  endsOn: FormControl<string>;
  latitude: FormControl<number | null>;
  longitude: FormControl<number | null>;
  category: FormControl<PlaceCategory | null>;
  address: FormControl<string>;
  notes: FormControl<string>;
  plannedOn: FormControl<string>;
  stops: FormArray<StopGroup>;
}>;

/** The control the map picker is writing to, once it is opened. */
interface PickerTarget {
  destination: DestinationGroup;
  stop: StopGroup | null;
}

/**
 * The editor for a trip and its route.
 *
 * The same editor is used to create a trip and to change one that already
 * exists: the route builder, the rules and the focus handling are written
 * once and behave the same in both.
 *
 * A trip is a main route of destinations, and each destination may hold stops.
 * The two are never peers: a stop's order is its order within its own
 * destination, and a stop never receives a main-route number.
 *
 * A trip handed to the form is laid out in it, in the order it is visited,
 * with its destinations and stops keeping their identities so that they can be
 * renamed, moved and rescheduled without losing their place.
 */
@Component({
  selector: 'app-trip-form',
  imports: [ReactiveFormsModule, LocationPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-form.html',
  styleUrl: './trip-form.css',
})
export class TripForm {
  private readonly tripsService = inject(TripsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly nameMaxLength = TRIP_NAME_MAX_LENGTH;
  protected readonly destinationMaxLength = TRIP_DESTINATION_MAX_LENGTH;
  protected readonly maxDestinations = TRIP_MAX_DESTINATIONS;
  protected readonly maxStopsPerDestination = TRIP_MAX_STOPS_PER_DESTINATION;

  /**
   * The trip being changed. Null when the form is making a new one.
   */
  readonly trip = input<Trip | null>(null);

  /** Emits the trip that was saved, whether it was made or changed. */
  readonly saved = output<Trip>();

  /** Emits when the reader backs out of a change. */
  readonly cancelled = output<void>();

  /** Emits the id of the trip that was deleted. */
  readonly deleted = output<string>();

  protected cancel(): void {
    this.cancelled.emit();
  }

  protected delete(): void {
    const trip = this.trip();

    if (trip === null || this.deleting()) {
      return;
    }

    const confirmed = window.confirm(
      `Delete "${trip.name}" and everything on its route? This can't be undone.`,
    );

    if (!confirmed) {
      return;
    }

    this.deleting.set(true);
    this.error.set(null);

    this.tripsService
      .delete(trip.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.deleting.set(false);
          this.deleted.emit(trip.id);
        },
        error: (failure: HttpErrorResponse) => {
          this.deleting.set(false);
          this.error.set(describeHttpError(failure));
        },
    });
  }

  readonly form = this.formBuilder.group(
    {
      name: ['', [Validators.required, Validators.maxLength(TRIP_NAME_MAX_LENGTH)]],
      destinations: this.formBuilder.array<DestinationGroup>(
        [this.destinationControl(null, '', '', '', null, null, [])],
        { validators: tripDestinationsValidator },
      ),
      startsOn: ['', Validators.required],
      endsOn: ['', Validators.required],
    },
    { validators: [tripDateRangeValidator, tripScheduleValidator] },
  );

  readonly submitting = signal(false);
  readonly submitted = signal(false);

  protected readonly deleting = signal(false);

  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  /** What the reader is told after a place is added, removed or moved. */
  protected readonly announcement = signal<string | null>(null);

  /**
   * The destinations that are expanded. It is kept outside the form so that
   * opening and closing never marks the form dirty, and the controls of a
   * collapsed destination stay in the form so nothing entered is lost.
   */
  private readonly expanded = signal<ReadonlySet<AbstractControl>>(new Set());

  /**
   * The place whose map picker is open, or null when none is. One picker
   * serves the whole form; this is what points it at the right place.
   */
  readonly pickerTarget = signal<PickerTarget | null>(null);

  /** What the picker is seeded with when it opens for the chosen place. */
  protected readonly pickerQuery = computed(() => {
    const target = this.pickerTarget();
    const control = target?.stop ?? target?.destination;

    return control?.controls.name.value.trim() ?? '';
  });

  /** The coordinates the chosen place already has, if it has any. */
  protected readonly pickerCoordinates = computed<PlaceSelection | null>(() => {
    const target = this.pickerTarget();
    const control = target?.stop ?? target?.destination;
    const latitude = control?.controls.latitude.value ?? null;
    const longitude = control?.controls.longitude.value ?? null;

    if (latitude === null || longitude === null) {
      return null;
    }

    return { name: control?.controls.name.value.trim() ?? '', latitude, longitude };
  });

  constructor() {
    const first = this.destinations.at(0);

    if (first !== undefined) {
      this.expanded.set(new Set([first]));
    }

    // A trip handed to the form is laid out in it. The places keep the
    // identities they had, so that the API can tell a renamed place from a
    // new one.
    effect(() => {
      const trip = this.trip();

      if (trip !== null) {
        this.load(trip);
      }
    });
  }

  protected get destinations(): FormArray<DestinationGroup> {
    return this.form.controls.destinations;
  }

  /** Whether the reader has changed anything. */
  isDirty(): boolean {
    return this.form.dirty;
  }

  private stopControl(
    id: string | null,
    name: string,
    startsOn: string,
    endsOn: string,
    latitude: number | null = null,
    longitude: number | null = null,
    category: PlaceCategory | null = null,
    address = '',
    notes = '',
    plannedOn = '',
  ): StopGroup {
    return this.formBuilder.group(
      {
        id: this.formBuilder.control<string | null>(id),
        name: this.formBuilder.control(name, {
          validators: [
            Validators.required,
            Validators.maxLength(TRIP_DESTINATION_MAX_LENGTH),
            nonBlankValidator,
          ],
        }),
        startsOn: this.formBuilder.control(startsOn),
        endsOn: this.formBuilder.control(endsOn),
        latitude: this.formBuilder.control<number | null>(latitude),
        longitude: this.formBuilder.control<number | null>(longitude),
        category: this.formBuilder.control<PlaceCategory | null>(category),
        address: this.formBuilder.control(address),
        notes: this.formBuilder.control(notes),
        plannedOn: this.formBuilder.control(plannedOn),
      },
      { validators: destinationDatesValidator },
    );
  }

  private destinationControl(
    id: string | null,
    name: string,
    startsOn: string,
    endsOn: string,
    latitude: number | null = null,
    longitude: number | null = null,
    stops: StopGroup[] = [],
    category: PlaceCategory | null = null,
    address = '',
    notes = '',
    plannedOn = '',
  ): DestinationGroup {
    return this.formBuilder.group(
      {
        id: this.formBuilder.control<string | null>(id),
        name: this.formBuilder.control(name, {
          validators: [
            Validators.required,
            Validators.maxLength(TRIP_DESTINATION_MAX_LENGTH),
            nonBlankValidator,
          ],
        }),
        startsOn: this.formBuilder.control(startsOn),
        endsOn: this.formBuilder.control(endsOn),
        latitude: this.formBuilder.control<number | null>(latitude),
        longitude: this.formBuilder.control<number | null>(longitude),
        category: this.formBuilder.control<PlaceCategory | null>(category),
        address: this.formBuilder.control(address),
        notes: this.formBuilder.control(notes),
        plannedOn: this.formBuilder.control(plannedOn),
        stops: this.formBuilder.array<StopGroup>(stops, { validators: tripStopsValidator }),
      },
      { validators: destinationDatesValidator },
    );
  }

  /**
   * Lays a trip out in the form: its name, its dates and its destinations,
   * each with the stops within it.
   */
  private load(trip: Trip): void {
    const destinations = trip.destinations
      .slice()
      .sort((left, right) => left.sortOrder - right.sortOrder);

    this.form.controls.destinations.clear();

    const expanded = new Set<AbstractControl>();

    for (const destination of destinations) {
      const stops = stopsOf(destination)
        .slice()
        .sort((left, right) => left.sortOrder - right.sortOrder)
        .map((stop) =>
          this.stopControl(
            stop.id,
            stop.name,
            stop.startsOn ?? '',
            stop.endsOn ?? '',
            stop.latitude ?? null,
            stop.longitude ?? null,
            stop.category ?? null,
            stop.address ?? '',
            stop.notes ?? '',
            stop.plannedOn ?? '',
          ),
        );

      const control = this.destinationControl(
        destination.id,
        destination.name,
        destination.startsOn ?? '',
        destination.endsOn ?? '',
        destination.latitude ?? null,
        destination.longitude ?? null,
        stops,
        destination.category ?? null,
        destination.address ?? '',
        destination.notes ?? '',
        destination.plannedOn ?? '',
      );

      this.form.controls.destinations.push(control);

      if (stops.length > 0) {
        expanded.add(control);
      }
    }

    if (expanded.size === 0) {
      const first = this.form.controls.destinations.at(0);

      if (first !== undefined) {
        expanded.add(first);
      }
    }

    this.expanded.set(expanded);

    this.form.patchValue({
      name: trip.name,
      startsOn: trip.startsOn,
      endsOn: trip.endsOn,
    });

    this.form.markAsPristine();
    this.submitted.set(false);
    this.deleting.set(false);
    this.error.set(null);
    this.success.set(null);
  }

  protected submit(): void {
    this.submitted.set(true);
    this.error.set(null);
    this.success.set(null);

    if (this.form.invalid) {
      // A collapsed destination must never hide the reason the form cannot be
      // sent, so every destination is opened before the errors are shown.
      this.expanded.set(new Set(this.form.controls.destinations.controls));
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    const trip = this.trip();

    if (trip === null) {
      this.tripsService
        .create(this.toCreateRequest())
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (created) => {
            this.submitting.set(false);
            this.form.reset();
            this.success.set(`Created "${created.name}".`);
            this.saved.emit(created);
          },
          error: (failure: HttpErrorResponse) => {
            this.submitting.set(false);
            this.error.set(describeHttpError(failure));
          },
        });

      return;
    }

    this.tripsService
      .update(trip.id, this.toUpdateRequest())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.submitting.set(false);
          this.success.set(`Saved "${updated.name}".`);
          this.saved.emit(updated);
        },
        error: (failure: HttpErrorResponse) => {
          this.submitting.set(false);
          this.error.set(describeHttpError(failure));
        },
      });
  }

  private toCreateRequest(): CreateTrip {
    return {
      name: this.form.controls.name.value,
      startsOn: this.form.controls.startsOn.value,
      endsOn: this.form.controls.endsOn.value,
      destinations: this.destinations.controls.map((control) => ({
        name: control.controls.name.value,
        startsOn: control.controls.startsOn.value || null,
        endsOn: control.controls.endsOn.value || null,
        latitude: control.controls.latitude.value,
        longitude: control.controls.longitude.value,
        ...this.detailsOf(control.controls),
        stops: control.controls.stops.controls.map((stop) => ({
          name: stop.controls.name.value,
          startsOn: stop.controls.startsOn.value || null,
          endsOn: stop.controls.endsOn.value || null,
          latitude: stop.controls.latitude.value,
          longitude: stop.controls.longitude.value,
          ...this.detailsOf(stop.controls),
        })),
      })),
    };
  }

  private toUpdateRequest(): UpdateTrip {
    return {
      name: this.form.controls.name.value,
      startsOn: this.form.controls.startsOn.value,
      endsOn: this.form.controls.endsOn.value,
      destinations: this.destinations.controls.map((control) => ({
        id: control.controls.id.value,
        name: control.controls.name.value,
        startsOn: control.controls.startsOn.value || null,
        endsOn: control.controls.endsOn.value || null,
        latitude: control.controls.latitude.value,
        longitude: control.controls.longitude.value,
        ...this.detailsOf(control.controls),
        stops: control.controls.stops.controls.map((stop) => ({
          id: stop.controls.id.value,
          name: stop.controls.name.value,
          startsOn: stop.controls.startsOn.value || null,
          endsOn: stop.controls.endsOn.value || null,
          latitude: stop.controls.latitude.value,
          longitude: stop.controls.longitude.value,
          ...this.detailsOf(stop.controls),
        })),
      })),
    };
  }

  /**
   * The details a stop or destination carries, when the reader has set them.
   * They are omitted when they are empty: the server reads a missing optional
   * field as "not set", and a form that never showed these controls must not
   * send a different answer from one that did.
   */
  private detailsOf(controls: {
    category: FormControl<PlaceCategory | null>;
    address: FormControl<string>;
    notes: FormControl<string>;
    plannedOn: FormControl<string>;
  }): {
    category?: PlaceCategory;
    address?: string;
    notes?: string;
    plannedOn?: string;
  } {
    const details: {
      category?: PlaceCategory;
      address?: string;
      notes?: string;
      plannedOn?: string;
    } = {};

    const category = controls.category.value;

    if (category !== null) {
      details.category = category;
    }

    const address = controls.address.value.trim();

    if (address.length > 0) {
      details.address = address;
    }

    const notes = controls.notes.value.trim();

    if (notes.length > 0) {
      details.notes = notes;
    }

    const plannedOn = controls.plannedOn.value;

    if (plannedOn.length > 0) {
      details.plannedOn = plannedOn;
    }

    return details;
  }

  protected addDestination(): void {
    if (this.destinations.length >= this.maxDestinations) {
      return;
    }

    const control = this.destinationControl(null, '', '', '', null, null, []);

    this.destinations.push(control);
    this.expand(control);

    const position = this.destinations.length;
    this.announcement.set(`Added destination ${position}.`);
    this.focusDestination(position - 1);
  }

  protected removeDestination(index: number): void {
    if (this.destinations.length <= 1) {
      return;
    }

    const control = this.destinations.at(index);
    const name = this.destinationLabel(index);
    const stopCount = control?.controls.stops.length ?? 0;

    this.destinations.removeAt(index);

    const next = new Set(this.expanded());
    if (control !== undefined) {
      next.delete(control);
    }
    this.expanded.set(next);

    this.announcement.set(
      stopCount > 0
        ? `Removed ${name} and its ${stopCount} ${stopCount === 1 ? 'stop' : 'stops'}.`
        : `Removed ${name}.`,
    );

    this.focusDestination(Math.min(index, this.destinations.length - 1));
  }

  protected moveUp(index: number): void {
    if (index <= 0) {
      return;
    }

    this.moveTo(index, index - 1, 'up');
  }

  protected moveDown(index: number): void {
    if (index >= this.destinations.length - 1) {
      return;
    }

    this.moveTo(index, index + 1, 'down');
  }

  /**
   * Moves a destination and keeps the order of the array in step. The template
   * tracks each control rather than its position, so the row is moved in the
   * document, and its stops move with it.
   */
  private moveTo(from: number, to: number, action: 'up' | 'down'): void {
    const control = this.destinations.at(from);

    this.destinations.removeAt(from);
    this.destinations.insert(to, control);

    const name = this.destinationLabel(to);
    const position = to + 1;

    this.announcement.set(`Moved ${name} to position ${position}.`);

    // Moving a row in the document takes the focus with it, so the button
    // the reader pressed is given the focus back in its new position. They
    // can carry on moving the same destination.
    this.focusAction(to, action);
  }

  protected isExpanded(index: number): boolean {
    const control = this.destinations.at(index);

    return control !== undefined && this.expanded().has(control);
  }

  protected toggleDestination(index: number): void {
    const control = this.destinations.at(index);

    if (control === undefined) {
      return;
    }

    const next = new Set(this.expanded());

    if (next.has(control)) {
      next.delete(control);
    } else {
      next.add(control);
    }

    this.expanded.set(next);
  }

  private expand(control: AbstractControl): void {
    const next = new Set(this.expanded());
    next.add(control);
    this.expanded.set(next);
  }

  protected stopsAt(index: number): FormArray<StopGroup> | undefined {
    return this.destinations.at(index)?.controls.stops;
  }

  protected stopCount(index: number): number {
    return this.stopsAt(index)?.length ?? 0;
  }

  protected addStop(destinationIndex: number): void {
    const destination = this.destinations.at(destinationIndex);

    if (destination === undefined || destination.controls.stops.length >= this.maxStopsPerDestination) {
      return;
    }

    destination.controls.stops.push(this.stopControl(null, '', '', ''));
    this.expand(destination);

    const stopIndex = destination.controls.stops.length - 1;

    this.announcement.set(`Added a stop at ${this.destinationLabel(destinationIndex)}.`);
    this.focusStop(destinationIndex, stopIndex);
  }

  protected removeStop(destinationIndex: number, stopIndex: number): void {
    const destination = this.destinations.at(destinationIndex);

    if (destination === undefined) {
      return;
    }

    const name = this.stopLabel(destinationIndex, stopIndex);

    destination.controls.stops.removeAt(stopIndex);

    this.announcement.set(`Removed ${name}.`);
    this.focusStop(
      destinationIndex,
      Math.min(stopIndex, destination.controls.stops.length - 1),
    );
  }

  protected moveStopUp(destinationIndex: number, stopIndex: number): void {
    if (stopIndex <= 0) {
      return;
    }

    this.moveStop(destinationIndex, stopIndex, stopIndex - 1, 'up');
  }

  protected moveStopDown(destinationIndex: number, stopIndex: number): void {
    const destination = this.destinations.at(destinationIndex);

    if (destination === undefined || stopIndex >= destination.controls.stops.length - 1) {
      return;
    }

    this.moveStop(destinationIndex, stopIndex, stopIndex + 1, 'down');
  }

  private moveStop(
    destinationIndex: number,
    from: number,
    to: number,
    action: 'up' | 'down',
  ): void {
    const stops = this.destinations.at(destinationIndex)?.controls.stops;

    if (stops === undefined) {
      return;
    }

    const control = stops.at(from);

    stops.removeAt(from);
    stops.insert(to, control);

    const name = this.stopLabel(destinationIndex, to);

    this.announcement.set(`Moved ${name} to stop position ${to + 1}.`);
    this.focusStopAction(destinationIndex, to, action);
  }

  protected stopsPanelId(index: number): string {
    return `trip-destination-${index}-stops`;
  }

  protected stopsToggleId(index: number): string {
    return `trip-destination-${index}-stops-toggle`;
  }

  protected stopInputId(index: number, stopIndex?: number): string {
    return stopIndex === undefined
      ? `trip-destination-${index}`
      : `trip-stop-${index}-${stopIndex}`;
  }

  protected stopDateInputId(
    index: number,
    field: 'startsOn' | 'endsOn',
    stopIndex?: number,
  ): string {
    return stopIndex === undefined
      ? `trip-destination-${index}-${field}`
      : `trip-stop-${index}-${stopIndex}-${field}`;
  }

  protected stopActionId(
    index: number,
    action: 'up' | 'down' | 'remove',
    stopIndex?: number,
  ): string {
    return stopIndex === undefined
      ? `trip-destination-${index}-${action}`
      : `trip-stop-${index}-${stopIndex}-${action}`;
  }

  protected stopMapButtonId(index: number, stopIndex?: number): string {
    return stopIndex === undefined
      ? `trip-destination-${index}-map`
      : `trip-stop-${index}-${stopIndex}-map`;
  }

  /** Opens the map picker for a destination, or for one of its stops. */
  protected openPicker(index: number, stopIndex?: number): void {
    const destination = this.destinations.at(index);

    if (destination === undefined) {
      return;
    }

    this.pickerTarget.set({
      destination,
      stop: stopIndex === undefined ? null : (destination.controls.stops.at(stopIndex) ?? null),
    });
  }

  /** Closes the map picker without changing anything. */
  protected closePicker(): void {
    this.pickerTarget.set(null);
  }

  /**
   * Takes the place chosen from the map and fills the destination or stop the
   * picker was opened for. Only its name and its coordinates are written, so
   * its dates, its place in the route and every other place are left alone.
   */
  protected onPlaceChosen(place: PlaceSelection): void {
    const target = this.pickerTarget();
    const control = target?.stop ?? target?.destination;

    if (control) {
      control.patchValue({
        name: place.name,
        latitude: place.latitude,
        longitude: place.longitude,
      });

      // Choosing a place is a change the reader would not want to lose.
      this.form.markAsDirty();
    }

    this.pickerTarget.set(null);
  }

  /**
   * A name typed by hand no longer matches the place that was chosen from
   * the map, so the coordinates are dropped. Otherwise a place would keep the
   * coordinates of one place while showing the name of another.
   */
  protected onNameTyped(index: number, stopIndex?: number): void {
    const destination = this.destinations.at(index);
    const control =
      stopIndex === undefined ? destination : destination?.controls.stops.at(stopIndex);

    if (control === undefined || control === null) {
      return;
    }

    const latitude = control.controls.latitude.value;
    const longitude = control.controls.longitude.value;

    if (latitude !== null || longitude !== null) {
      control.patchValue({ latitude: null, longitude: null });
    }
  }

  protected errorFor(field: 'name' | 'startsOn' | 'endsOn'): string | null {
    const control = this.form.controls[field];

    if (!control.touched || !control.errors) {
      return null;
    }

    if (control.hasError('required')) {
      return 'This field is required.';
    }

    if (control.hasError('maxlength')) {
      return `Use at most ${TRIP_NAME_MAX_LENGTH} characters.`;
    }

    return null;
  }

  /** The message for one destination, if its name is in trouble. */
  protected destinationError(index: number): string | null {
    const name = this.destinations.at(index)?.controls.name;

    if (!name || !name.touched || !name.errors) {
      return null;
    }

    if (name.hasError('required') || name.hasError('blank')) {
      return 'This field is required.';
    }

    if (name.hasError('maxlength')) {
      return `Use at most ${TRIP_DESTINATION_MAX_LENGTH} characters.`;
    }

    return null;
  }

  /** The message for one stop, if its name is in trouble. */
  protected stopError(index: number, stopIndex: number): string | null {
    const name = this.destinations.at(index)?.controls.stops.at(stopIndex)?.controls.name;

    if (!name || !name.touched || !name.errors) {
      return null;
    }

    if (name.hasError('required') || name.hasError('blank')) {
      return 'This field is required.';
    }

    if (name.hasError('maxlength')) {
      return `Use at most ${TRIP_DESTINATION_MAX_LENGTH} characters.`;
    }

    return null;
  }

  /**
   * The message for a destination whose own dates are the wrong way round.
   */
  protected destinationDatesError(index: number): string | null {
    const control = this.destinations.at(index);

    if (!control || !control.touched || !control.errors) {
      return null;
    }

    const errors = control.errors as { destinationDates?: { message?: string } } | null;
    const message = errors?.destinationDates?.message;

    return typeof message === 'string' ? message : null;
  }

  /** The message for a stop whose own dates are the wrong way round. */
  protected stopDatesError(index: number, stopIndex: number): string | null {
    const control = this.destinations.at(index)?.controls.stops.at(stopIndex);

    if (!control || !control.touched || !control.errors) {
      return null;
    }

    const errors = control.errors as { destinationDates?: { message?: string } } | null;
    const message = errors?.destinationDates?.message;

    return typeof message === 'string' ? message : null;
  }

  /** The message for a destination's stops as a whole, if they are in trouble. */
  protected stopsError(index: number): string | null {
    const control = this.destinations.at(index)?.controls.stops;

    if (!control) {
      return null;
    }

    const errors = control.errors as { tripStops?: { message?: string } } | null;
    const message = errors?.tripStops?.message;

    return typeof message === 'string' ? message : null;
  }

  /**
   * The message for the destination or stop whose dates contradict the route
   * it is on, or the trip or destination it belongs to.
   */
  protected scheduleError(index: number, stopIndex?: number): string | null {
    const errors = this.form.errors as
      | { tripSchedule?: { message?: string; index?: number; stopIndex?: number } }
      | null;
    const problem = errors?.tripSchedule;

    if (!problem || problem.index !== index) {
      return null;
    }

    if (stopIndex === undefined) {
      return problem.stopIndex === undefined && typeof problem.message === 'string'
        ? problem.message
        : null;
    }

    return problem.stopIndex === stopIndex && typeof problem.message === 'string'
      ? problem.message
      : null;
  }

  /**
   * The elements that describe a destination: the messages that belong to it,
   * if there are any.
   */
  protected destinationDescribedBy(index: number): string | null {
    const ids: string[] = [];

    if (this.destinationError(index) !== null) {
      ids.push(`trip-destination-error-${index}`);
    }

    if (this.destinationDatesError(index) !== null) {
      ids.push(`trip-destination-dates-error-${index}`);
    }

    if (this.scheduleError(index) !== null) {
      ids.push(`trip-destination-schedule-${index}`);
    }

    return ids.length > 0 ? ids.join(' ') : null;
  }

  /**
   * The elements that describe a stop: the messages that belong to it, if
   * there are any.
   */
  protected stopDescribedBy(index: number, stopIndex: number): string | null {
    const ids: string[] = [];

    if (this.stopError(index, stopIndex) !== null) {
      ids.push(`trip-stop-error-${index}-${stopIndex}`);
    }

    if (this.stopDatesError(index, stopIndex) !== null) {
      ids.push(`trip-stop-dates-error-${index}-${stopIndex}`);
    }

    if (this.scheduleError(index, stopIndex) !== null) {
      ids.push(`trip-stop-schedule-${index}-${stopIndex}`);
    }

    return ids.length > 0 ? ids.join(' ') : null;
  }

  /** The message for the route as a whole, if the route is in trouble. */
  protected routeError(): string | null {
    if (!this.submitted()) {
      return null;
    }

    const errors = this.destinations.errors as { tripDestinations?: { message?: string } } | null;
    const message = errors?.tripDestinations?.message;

    return typeof message === 'string' ? message : null;
  }

  protected dateRangeError(): string | null {
    if (!this.submitted()) {
      return null;
    }

    const errors = this.form.errors as { tripDates?: { message?: string } } | null;
    const message = errors?.tripDates?.message;

    return typeof message === 'string' ? message : null;
  }

  /**
   * The elements that describe a date field: its own hint, plus the shared
   * date range error when there is one.
   */
  protected dateDescribedBy(field: 'startsOn' | 'endsOn'): string {
    const hint = field === 'startsOn' ? 'trip-starts-on-hint' : 'trip-ends-on-hint';

    return this.dateRangeError() ? `trip-dates-error ${hint}` : hint;
  }

  /** The accessible name of a destination, used by the buttons beside it. */
  protected destinationLabel(index: number): string {
    const name = this.destinations.at(index)?.controls.name.value.trim();

    return name && name.length > 0 ? name : `destination ${index + 1}`;
  }

  /** The accessible name of a stop, used by the buttons beside it. */
  protected stopLabel(index: number, stopIndex: number): string {
    const name = this.destinations
      .at(index)
      ?.controls.stops.at(stopIndex)
      ?.controls.name.value.trim();

    return name && name.length > 0 ? name : `stop ${stopIndex + 1}`;
  }

  /**
   * Puts the caret in a destination, so that adding or removing one does not
   * leave the focus on a button that has just gone.
   */
  private focusDestination(index: number): void {
    this.focusElement(this.stopInputId(index), true);
  }

  /** Puts the caret in a stop. */
  private focusStop(index: number, stopIndex: number): void {
    this.focusElement(this.stopInputId(index, stopIndex), true);
  }

  /**
   * Gives the focus back to the button the reader pressed, now in its new
   * position.
   */
  private focusAction(index: number, action: 'up' | 'down'): void {
    this.focusElement(this.stopActionId(index, action), false);
  }

  private focusStopAction(
    index: number,
    stopIndex: number,
    action: 'up' | 'down',
  ): void {
    this.focusElement(this.stopActionId(index, action, stopIndex), false);
  }

  private focusElement(id: string, select: boolean): void {
    setTimeout(() => {
      const element = document.getElementById(id) as HTMLInputElement | HTMLButtonElement | null;

      element?.focus();

      if (select && element instanceof HTMLInputElement) {
        element.select();
      }
    });
  }
}
