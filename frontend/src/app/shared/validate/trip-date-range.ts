import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { toUtcTimestamp } from './iso-date';

/**
 * Validates that a trip does not end before it starts.
 *
 * This mirrors the domain rule enforced by the API
 * (Tabiji.Domain.Trips.Trip). The dates are compared as UTC timestamps so that
 * no timezone or locale can shift the result.
 *
 * Returns null when the dates are acceptable.
 */
export function validateTripDates(startsOn: string, endsOn: string): string | null {
  const start = toUtcTimestamp(startsOn);
  const end = toUtcTimestamp(endsOn);

  if (start === null || end === null) {
    return null;
  }

  return end < start ? 'The end date must be on or after the start date.' : null;
}

/**
 * Group level validator for a form that holds `startsOn` and `endsOn` controls.
 * The message is exposed as `errors.tripDates.message` so the form can show it.
 */
export const tripDateRangeValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const startsOn = control.get('startsOn')?.value;
  const endsOn = control.get('endsOn')?.value;

  if (typeof startsOn !== 'string' || typeof endsOn !== 'string') {
    return null;
  }

  const message = validateTripDates(startsOn, endsOn);

  return message ? { tripDates: { message } } : null;
};
