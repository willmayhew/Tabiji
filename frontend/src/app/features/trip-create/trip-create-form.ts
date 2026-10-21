import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import {
  Trip,
  TRIP_DESTINATION_MAX_LENGTH,
  TRIP_MAX_DESTINATIONS,
  TRIP_NAME_MAX_LENGTH,
} from '../../core/models/trip';
import { tripDateRangeValidator } from './trip-date-range';
import { nonBlankValidator, tripDestinationsValidator } from './trip-destinations';

@Component({
  selector: 'app-trip-create-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-create-form.html',
  styleUrl: './trip-create-form.css',
})
export class TripCreateForm {
  private readonly tripsService = inject(TripsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly nameMaxLength = TRIP_NAME_MAX_LENGTH;
  protected readonly destinationMaxLength = TRIP_DESTINATION_MAX_LENGTH;
  protected readonly maxDestinations = TRIP_MAX_DESTINATIONS;

  /** Emits the trip that was created, so the page can refresh its list. */
  readonly created = output<Trip>();

  readonly form = this.formBuilder.group(
    {
      name: ['', [Validators.required, Validators.maxLength(TRIP_NAME_MAX_LENGTH)]],
      destinations: this.formBuilder.array([this.destinationControl('')], {
        validators: tripDestinationsValidator,
      }),
      startsOn: ['', Validators.required],
      endsOn: ['', Validators.required],
    },
    { validators: tripDateRangeValidator },
  );

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  /** What the reader is told after a stop is added, removed or moved. */
  protected readonly announcement = signal<string | null>(null);

  protected get destinations() {
    return this.form.controls.destinations;
  }

  private destinationControl(name: string) {
    return this.formBuilder.control(name, {
      validators: [
        Validators.required,
        Validators.maxLength(TRIP_DESTINATION_MAX_LENGTH),
        nonBlankValidator,
      ],
    });
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

    const request = {
      name: this.form.controls.name.value,
      destinations: this.destinations.controls.map((control) => control.value),
      startsOn: this.form.controls.startsOn.value,
      endsOn: this.form.controls.endsOn.value,
    };

    this.tripsService
      .create(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (trip) => {
          this.submitting.set(false);
          this.form.reset();
          this.success.set(`Created "${trip.name}".`);
          this.created.emit(trip);
        },
        error: (failure: HttpErrorResponse) => {
          this.submitting.set(false);
          this.error.set(describeHttpError(failure));
        },
      });
  }

  protected addDestination(): void {
    if (this.destinations.length >= this.maxDestinations) {
      return;
    }

    this.destinations.push(this.destinationControl(''));

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

  protected destinationInputId(index: number): string {
    return `trip-destination-${index}`;
  }

  protected destinationActionId(index: number, action: 'up' | 'down' | 'remove'): string {
    return `trip-destination-${index}-${action}`;
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
    const control = this.destinations.at(index);

    if (!control.touched || !control.errors) {
      return null;
    }

    if (control.hasError('required') || control.hasError('blank')) {
      return 'This field is required.';
    }

    if (control.hasError('maxlength')) {
      return `Use at most ${TRIP_DESTINATION_MAX_LENGTH} characters.`;
    }

    return null;
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
    const value = this.destinations.at(index)?.value.trim();

    return value && value.length > 0 ? value : null;
  }

  /**
   * Puts the caret in a stop, so that adding or removing one does not leave
   * the focus on a button that has just gone.
   */
  private focusDestination(index: number): void {
    this.focusElement(this.destinationInputId(index), true);
  }

  /**
   * Gives the focus back to the button the reader pressed, now in its new
   * position.
   */
  private focusAction(index: number, action: 'up' | 'down'): void {
    this.focusElement(this.destinationActionId(index, action), false);
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
