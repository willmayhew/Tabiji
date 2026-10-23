import { HttpErrorResponse } from '@angular/common/http';
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
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { PlaceSelection } from '../../core/models/place';
import {
  CreateTrip,
  Trip,
  TRIP_DESTINATION_MAX_LENGTH,
  TRIP_MAX_DESTINATIONS,
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
} from '../validate/trip-destinations';

/**
 * The editor for a trip and its route.
 *
 * The same editor is used to create a trip and to change one that already
 * exists: the route builder, the rules and the focus handling are written
 * once and behave the same in both.
 *
 * A trip handed to the form is laid out in it, in the order it is visited,
 * with its stops keeping their identities so that they can be renamed, moved
 * and rescheduled without losing their place.
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

  /**
   * The trip being changed. Null when the form is making a new one.
   */
  readonly trip = input<Trip | null>(null);

  /** Emits the trip that was saved, whether it was made or changed. */
  readonly saved = output<Trip>();

  /** Emits when the reader backs out of a change. */
  readonly cancelled = output<void>();

  protected cancel(): void {
    this.cancelled.emit();
  }

  readonly form = this.formBuilder.group(
    {
      name: ['', [Validators.required, Validators.maxLength(TRIP_NAME_MAX_LENGTH)]],
      destinations: this.formBuilder.array([this.destinationControl(null, '', '', '')], {
        validators: tripDestinationsValidator,
      }),
      startsOn: ['', Validators.required],
      endsOn: ['', Validators.required],
    },
    { validators: [tripDateRangeValidator, tripScheduleValidator] },
  );

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  /** What the reader is told after a stop is added, removed or moved. */
  protected readonly announcement = signal<string | null>(null);

  /**
   * The stop whose map picker is open, or null when none is. One picker
   * serves the whole form; this is what points it at the right stop.
   */
  readonly pickerIndex = signal<number | null>(null);

  /** What the picker is seeded with when it opens for the chosen stop. */
  protected readonly pickerQuery = computed(() => {
    const index = this.pickerIndex();
    const control = index === null ? null : this.destinations.at(index);

    return control?.controls.name.value.trim() ?? '';
  });

  /** The coordinates the chosen stop already has, if it has any. */
  protected readonly pickerCoordinates = computed<PlaceSelection | null>(() => {
    const index = this.pickerIndex();
    const control = index === null ? null : this.destinations.at(index);
    const latitude = control?.controls.latitude.value ?? null;
    const longitude = control?.controls.longitude.value ?? null;

    if (latitude === null || longitude === null) {
      return null;
    }

    return { name: control?.controls.name.value.trim() ?? '', latitude, longitude };
  });

  constructor() {
    // A trip handed to the form is laid out in it. The stops keep the
    // identities they had, so that the API can tell a renamed stop from a
    // new one.
    effect(() => {
      const trip = this.trip();

      if (trip !== null) {
        this.load(trip);
      }
    });
  }

  protected get destinations() {
    return this.form.controls.destinations;
  }

  /** Whether the reader has changed anything. */
  isDirty(): boolean {
    return this.form.dirty;
  }

  private destinationControl(
    id: string | null,
    name: string,
    startsOn: string,
    endsOn: string,
    latitude: number | null = null,
    longitude: number | null = null,
  ) {
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
      },
      { validators: destinationDatesValidator },
    );
  }

  /**
   * Lays a trip out in the form: its name, its dates and its stops, in the
   * order they are visited.
   */
  private load(trip: Trip): void {
    const stops = trip.destinations.slice().sort((left, right) => left.sortOrder - right.sortOrder);

    this.form.controls.destinations.clear();

    for (const stop of stops) {
      this.form.controls.destinations.push(
        this.destinationControl(
          stop.id,
          stop.name,
          stop.startsOn ?? '',
          stop.endsOn ?? '',
          stop.latitude ?? null,
          stop.longitude ?? null,
        ),
      );
    }

    this.form.patchValue({
      name: trip.name,
      startsOn: trip.startsOn,
      endsOn: trip.endsOn,
    });

    this.form.markAsPristine();
    this.submitted.set(false);
    this.error.set(null);
    this.success.set(null);
  }

  protected submit(): void {
    this.submitted.set(true);
    this.error.set(null);
    this.success.set(null);

    if (this.form.invalid) {
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
      })),
    };
  }

  protected addDestination(): void {
    if (this.destinations.length >= this.maxDestinations) {
      return;
    }

    this.destinations.push(this.destinationControl(null, '', '', ''));

    const position = this.destinations.length;
    this.announcement.set(`Added destination ${position}.`);
    this.focusDestination(position - 1);
  }

  protected removeDestination(index: number): void {
    if (this.destinations.length <= 1) {
      return;
    }

    const name = this.nameOf(index);

    this.destinations.removeAt(index);

    this.announcement.set(name ? `Removed ${name}.` : `Removed destination ${index + 1}.`);
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
   * Moves a stop and keeps the order of the array in step. The template
   * tracks each control rather than its position, so the row is moved in the
   * document.
   */
  private moveTo(from: number, to: number, action: 'up' | 'down'): void {
    const control = this.destinations.at(from);

    this.destinations.removeAt(from);
    this.destinations.insert(to, control);

    const name = this.nameOf(to);
    const position = to + 1;

    this.announcement.set(
      name ? `Moved ${name} to position ${position}.` : `Moved destination to position ${position}.`,
    );

    // Moving a row in the document takes the focus with it, so the button
    // the reader pressed is given the focus back in its new position. They
    // can carry on moving the same stop.
    this.focusAction(to, action);
  }

  protected stopInputId(index: number): string {
    return `trip-destination-${index}`;
  }

  protected stopDateInputId(index: number, field: 'startsOn' | 'endsOn'): string {
    return `trip-destination-${index}-${field}`;
  }

  protected stopActionId(index: number, action: 'up' | 'down' | 'remove'): string {
    return `trip-destination-${index}-${action}`;
  }

  protected stopMapButtonId(index: number): string {
    return `trip-destination-${index}-map`;
  }

  /** Opens the map picker for the stop at `index`. */
  protected openPicker(index: number): void {
    this.pickerIndex.set(index);
  }

  /** Closes the map picker without changing anything. */
  protected closePicker(): void {
    this.pickerIndex.set(null);
  }

  /**
   * Takes the place chosen from the map and fills the stop the picker was
   * opened for. Only its name and its coordinates are written, so its dates,
   * its place in the route and every other stop are left alone.
   */
  protected onPlaceChosen(place: PlaceSelection): void {
    const index = this.pickerIndex();
    const control = index === null ? null : this.destinations.at(index);

    if (control) {
      control.patchValue({
        name: place.name,
        latitude: place.latitude,
        longitude: place.longitude,
      });

      // Choosing a place is a change the reader would not want to lose.
      this.form.markAsDirty();
    }

    this.pickerIndex.set(null);
  }

  /**
   * A name typed by hand no longer matches the place that was chosen from
   * the map, so the coordinates are dropped. Otherwise a stop would keep the
   * coordinates of one place while showing the name of another.
   */
  protected onNameTyped(index: number): void {
    const control = this.destinations.at(index);

    if (!control) {
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

  /** The message for one stop, if that stop is in trouble. */
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

  /**
   * The message for a stop whose own dates are the wrong way round.
   */
  protected destinationDatesError(index: number): string | null {
    const control = this.destinations.at(index);

    if (!control.touched || !control.errors) {
      return null;
    }

    const errors = control.errors as { destinationDates?: { message?: string } } | null;
    const message = errors?.destinationDates?.message;

    return typeof message === 'string' ? message : null;
  }

  /**
   * The message for the stop whose dates contradict the route it is on, or
   * the trip it belongs to.
   */
  protected scheduleError(index: number): string | null {
    const errors = this.form.errors as
      | { tripSchedule?: { message?: string; index?: number } }
      | null;
    const problem = errors?.tripSchedule;

    if (!problem || problem.index !== index) {
      return null;
    }

    return typeof problem.message === 'string' ? problem.message : null;
  }

  /**
   * The elements that describe a stop: the messages that belong to it, if
   * there are any.
   */
  protected stopDescribedBy(index: number): string | null {
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

  /** The accessible name of a stop, used by the buttons beside it. */
  protected stopName(index: number): string {
    const name = this.nameOf(index);

    return name ?? `destination ${index + 1}`;
  }

  private nameOf(index: number): string | null {
    const value = this.destinations.at(index)?.controls.name.value.trim();

    return value && value.length > 0 ? value : null;
  }

  /**
   * Puts the caret in a stop, so that adding or removing one does not leave
   * the focus on a button that has just gone.
   */
  private focusDestination(index: number): void {
    this.focusElement(this.stopInputId(index), true);
  }

  /**
   * Gives the focus back to the button the reader pressed, now in its new
   * position.
   */
  private focusAction(index: number, action: 'up' | 'down'): void {
    this.focusElement(this.stopActionId(index, action), false);
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
