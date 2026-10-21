import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { TRIP_MAX_DESTINATIONS } from '../../core/models/trip';
import { isIsoDate } from './iso-date';

/**
 * A stop of a route, as the rules see it.
 */
export interface TripStopValue {
  name: string;
  /** The planned first day, or null when it has not been decided. */
  startsOn: string | null;
  /** The planned last day, or null when it has not been decided. */
  endsOn: string | null;
}

/**
 * What is wrong with a route, and where.
 */
export interface TripScheduleProblem {
  /** The message shown to the reader. */
  message: string;
  /** The stop that is at fault. */
  index: number;
  /** The field of that stop that is wrong. */
  field: 'startsOn' | 'endsOn';
}

const longDate = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  timeZone: 'UTC',
});

/**
 * A date as it is written out, so that the message reads the same wherever
 * it is produced.
 */
function format(isoDate: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(isoDate);

  if (!match) {
    return isoDate;
  }

  const [, year, month, day] = match;

  return longDate.format(new Date(Date.UTC(Number(year), Number(month) - 1, Number(day))));
}

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
 * Validates the schedule of a route against the trip it belongs to.
 *
 * Every date must fall inside the trip's own dates, a stop may not end
 * before it starts, and the stops must be able to be visited in the order
 * they are given. Gaps between stops are fine: the days that belong to no
 * stop are the traveller's own.
 *
 * This mirrors the domain rule (Tabiji.Domain.Trips.TripSchedule) step for
 * step, and says the same thing in the same words, so that the message the
 * reader sees here is the message the API would have given.
 *
 * Returns null when the route is sound.
 */
export function validateTripSchedule(
  tripStartsOn: string,
  tripEndsOn: string,
  stops: readonly TripStopValue[],
): TripScheduleProblem | null {
  if (!isIsoDate(tripStartsOn) || !isIsoDate(tripEndsOn)) {
    return null;
  }

  for (let index = 0; index < stops.length; index += 1) {
    const stop = stops[index];

    const outside = outsideTrip(stop, tripStartsOn, tripEndsOn, index);

    if (outside !== null) {
      return outside;
    }

    const backwards = reversed(stop, index);

    if (backwards !== null) {
      return backwards;
    }
  }

  return outOfOrder(stops, tripStartsOn);
}

/**
 * Checks that a stop's dates fall inside the trip's own dates.
 */
function outsideTrip(
  stop: TripStopValue,
  tripStartsOn: string,
  tripEndsOn: string,
  index: number,
): TripScheduleProblem | null {
  const startsOn = stop.startsOn;
  const endsOn = stop.endsOn;

  if (startsOn !== null && isIsoDate(startsOn)) {
    if (startsOn < tripStartsOn) {
      return {
        index,
        field: 'startsOn',
        message: `"${stop.name}" starts on ${format(startsOn)}, which is before the trip starts on ${format(tripStartsOn)}.`,
      };
    }

    if (startsOn > tripEndsOn) {
      return {
        index,
        field: 'startsOn',
        message: `"${stop.name}" starts on ${format(startsOn)}, which is after the trip ends on ${format(tripEndsOn)}.`,
      };
    }
  }

  if (endsOn !== null && isIsoDate(endsOn)) {
    if (endsOn < tripStartsOn) {
      return {
        index,
        field: 'endsOn',
        message: `"${stop.name}" ends on ${format(endsOn)}, which is before the trip starts on ${format(tripStartsOn)}.`,
      };
    }

    if (endsOn > tripEndsOn) {
      return {
        index,
        field: 'endsOn',
        message: `"${stop.name}" ends on ${format(endsOn)}, which is after the trip ends on ${format(tripEndsOn)}.`,
      };
    }
  }

  return null;
}

/**
 * Checks that a stop does not end before it starts.
 */
function reversed(stop: TripStopValue, index: number): TripScheduleProblem | null {
  const startsOn = stop.startsOn;
  const endsOn = stop.endsOn;

  if (
    startsOn !== null &&
    endsOn !== null &&
    isIsoDate(startsOn) &&
    isIsoDate(endsOn) &&
    endsOn < startsOn
  ) {
    return {
      index,
      field: 'endsOn',
      message: `"${stop.name}" ends on ${format(endsOn)} and starts on ${format(startsOn)}.`,
    };
  }

  return null;
}

/**
 * Walks the route in order and keeps a floor: the earliest day the next
 * stop may begin.
 *
 * A stop that carries no dates establishes nothing and is stepped over. A
 * stop that carries a start date establishes that day, because its stay may
 * be as short as a single day. A stop that carries only an end date
 * establishes its end, because it must have begun at or before it.
 *
 * The floor only ever moves forward, so a stop is measured against every
 * dated stop before it and not only against its neighbour.
 */
function outOfOrder(
  stops: readonly TripStopValue[],
  tripStartsOn: string,
): TripScheduleProblem | null {
  let floor = tripStartsOn;
  let anchor: TripStopValue | null = null;

  for (let index = 0; index < stops.length; index += 1) {
    const stop = stops[index];
    const startsOn = stop.startsOn !== null && isIsoDate(stop.startsOn) ? stop.startsOn : null;
    const endsOn = stop.endsOn !== null && isIsoDate(stop.endsOn) ? stop.endsOn : null;

    if (startsOn !== null) {
      if (startsOn < floor) {
        return {
          index,
          field: 'startsOn',
          message: contradiction(stop, 'starts', startsOn, anchor, floor),
        };
      }

      floor = endsOn ?? startsOn;
    } else if (endsOn !== null) {
      if (endsOn < floor) {
        return {
          index,
          field: 'endsOn',
          message: contradiction(stop, 'ends', endsOn, anchor, floor),
        };
      }

      floor = endsOn;
    } else {
      // A stop with no dates says nothing about when the journey reaches
      // it, so it changes nothing.
      continue;
    }

    anchor = stop;
  }

  return null;
}

/**
 * The message for a stop that contradicts the route it is on. It names the
 * stop at fault and the stop it conflicts with, so the traveller can put it
 * right without guessing.
 */
function contradiction(
  stop: TripStopValue,
  verb: 'starts' | 'ends',
  date: string,
  anchor: TripStopValue | null,
  floor: string,
): string {
  const conflict =
    anchor === null
      ? `the trip starts on ${format(floor)}`
      : anchor.startsOn === null
        ? `"${anchor.name}", which comes before it, does not end until ${format(floor)}`
        : anchor.endsOn === null
          ? `"${anchor.name}", which comes before it, does not begin until ${format(floor)}`
          : `"${anchor.name}", which comes before it, is there until ${format(floor)}`;

  return `"${stop.name}" ${verb} on ${format(date)}, but ${conflict}.`;
}

/**
 * Group level validator for the array of stops. The message is exposed as
 * `errors.tripDestinations.message` so the form can show it.
 *
 * The stops in the array are groups, so the names are read out of them.
 */
export const tripDestinationsValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const stops = control.value;

  if (!Array.isArray(stops)) {
    return null;
  }

  const names = stops.map((stop) => {
    if (typeof stop === 'string') {
      return stop;
    }

    const name = (stop as { name?: unknown } | null)?.name;

    return typeof name === 'string' ? name : '';
  });

  const message = validateTripDestinations(names);

  return message ? { tripDestinations: { message } } : null;
};

/**
 * Group level validator for the whole form, checking the schedule of the
 * route against the trip's own dates.
 *
 * It sits on the form rather than on a stop because it needs the trip's dates
 * and the whole route. The error is exposed as `errors.tripSchedule` and
 * carries the stop that is at fault, so the message can be shown against it.
 */
export const tripScheduleValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const tripStartsOn = control.get('startsOn')?.value;
  const tripEndsOn = control.get('endsOn')?.value;
  const destinations = control.get('destinations')?.value;

  if (
    typeof tripStartsOn !== 'string' ||
    typeof tripEndsOn !== 'string' ||
    !Array.isArray(destinations)
  ) {
    return null;
  }

  const problem = validateTripSchedule(
    tripStartsOn,
    tripEndsOn,
    destinations as TripStopValue[],
  );

  return problem
    ? { tripSchedule: { message: problem.message, index: problem.index, field: problem.field } }
    : null;
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

/**
 * Rejects a stop whose end date is before its start date.
 *
 * This is the one rule about a stop that the stop can answer on its own.
 * The rest of the schedule is checked against the trip as a whole.
 */
export const destinationDatesValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const startsOn = control.get('startsOn')?.value;
  const endsOn = control.get('endsOn')?.value;

  if (
    typeof startsOn !== 'string' ||
    typeof endsOn !== 'string' ||
    startsOn.length === 0 ||
    endsOn.length === 0
  ) {
    return null;
  }

  if (!isIsoDate(startsOn) || !isIsoDate(endsOn)) {
    return null;
  }

  return endsOn < startsOn
    ? { destinationDates: { message: 'The end date must be on or after the start date.' } }
    : null;
};
