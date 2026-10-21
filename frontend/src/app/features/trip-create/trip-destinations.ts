import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { TRIP_MAX_DESTINATIONS } from '../../core/models/trip';

/**
 * Validates the route of a trip as a whole.
 *
 * This mirrors the domain rules enforced by the API
 * (Tabiji.Domain.Trips.Trip). The rule itself lives in the domain; the form
 * mirrors it so the reader gets immediate feedback, and the API remains the
 * authority.
 *
 * Returns null when the route is acceptable.
 */
export function validateTripDestinations(names: readonly string[]): string | null {
  if (names.length === 0) {
    return 'A trip must have at least one destination.';
  }

  if (names.length > TRIP_MAX_DESTINATIONS) {
    return `A trip cannot have more than ${TRIP_MAX_DESTINATIONS} destinations.`;
  }

  for (const name of names) {
    if (typeof name !== 'string' || name.trim().length === 0) {
      return 'The trip destination name is required.';
    }
  }

  return null;
}

/**
 * Group level validator for the array of stops. The message is exposed as
 * `errors.tripDestinations.message` so the form can show it.
 */
export const tripDestinationsValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const names = control.value;

  if (!Array.isArray(names)) {
    return null;
  }

  const message = validateTripDestinations(names as string[]);

  return message ? { tripDestinations: { message } } : null;
};

/**
 * Rejects a stop that is made of nothing but spaces.
 *
 * The built in required validator only rejects an empty value, so a stop of
 * spaces would pass it and only be caught by the route as a whole. This
 * marks the stop itself as wrong.
 */
export const nonBlankValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;

  if (typeof value !== 'string' || value.length === 0) {
    return null;
  }

  return value.trim().length === 0 ? { blank: true } : null;
};
