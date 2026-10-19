import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { Trip, TRIP_DESTINATION_MAX_LENGTH, TRIP_NAME_MAX_LENGTH } from '../../core/models/trip';
import { tripDateRangeValidator } from './trip-date-range';

type TripField = 'name' | 'destination' | 'startsOn' | 'endsOn';

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

  /** Emits the trip that was created, so the page can refresh its list. */
  readonly created = output<Trip>();

  readonly form = this.formBuilder.group(
    {
      name: ['', [Validators.required, Validators.maxLength(TRIP_NAME_MAX_LENGTH)]],
      destination: ['', [Validators.required, Validators.maxLength(TRIP_DESTINATION_MAX_LENGTH)]],
      startsOn: ['', Validators.required],
      endsOn: ['', Validators.required],
    },
    { validators: tripDateRangeValidator },
  );

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);

  protected submit(): void {
    this.submitted.set(true);
    this.error.set(null);
    this.success.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    this.tripsService
      .create(this.form.getRawValue())
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

  protected errorFor(field: TripField): string | null {
    const control = this.form.controls[field];

    if (!control.touched || !control.errors) {
      return null;
    }

    if (control.hasError('required')) {
      return 'This field is required.';
    }

    if (control.hasError('maxlength')) {
      const limit = field === 'name' ? TRIP_NAME_MAX_LENGTH : TRIP_DESTINATION_MAX_LENGTH;
      return `Use at most ${limit} characters.`;
    }

    return null;
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
}
