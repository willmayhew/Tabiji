import { formatTripDate } from '../format/trip-dates';
import { toUtcTimestamp } from './iso-date';

/**
 * Validates that the day a place is planned for is one the trip has.
 *
 * This mirrors the rule the API enforces
 * (Tabiji.Domain.Trips.TripSchedule.EnsureIsATripDay): a day is a day of the
 * trip when it falls on or after the first and on or before the last, both
 * ends included, exactly as a stop's own dates do. The dates are compared as
 * UTC timestamps so that no timezone or locale can shift the result.
 *
 * A place that has not been planned for a day is not a mistake, so a null day
 * is accepted. The API remains the authority: this is here so the reader is
 * told before the request is made.
 *
 * Returns `null` when the day is acceptable.
 */
export function validatePlaceDay(
  tripStartsOn: string,
  tripEndsOn: string,
  plannedOn: string | null,
  name: string,
): string | null {
  if (plannedOn === null) {
    return null;
  }

  const start = toUtcTimestamp(tripStartsOn);
  const end = toUtcTimestamp(tripEndsOn);
  const day = toUtcTimestamp(plannedOn);

  if (start === null || end === null || day === null) {
    return null;
  }

  const subject = name.trim().length === 0 ? 'A saved place' : name.trim();
  const planned = `“${subject}” is planned for ${formatTripDate(plannedOn) ?? plannedOn}`;

  if (day < start) {
    const startsOn = formatTripDate(tripStartsOn) ?? tripStartsOn;

    return `${planned}, which is before the trip starts on ${startsOn}.`;
  }

  if (day > end) {
    const endsOn = formatTripDate(tripEndsOn) ?? tripEndsOn;

    return `${planned}, which is after the trip ends on ${endsOn}.`;
  }

  return null;
}
