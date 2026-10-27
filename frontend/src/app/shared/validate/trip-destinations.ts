import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import {
  TRIP_DESTINATION_MAX_LENGTH,
  TRIP_MAX_DESTINATIONS,
  TRIP_MAX_STOPS_PER_DESTINATION,
} from '../../core/models/trip';
import { isIsoDate } from './iso-date';

/**
 * A place on a route, as the rules see it. A destination and a stop carry the
 * same three values.
 */
export interface TripStopValue {
  name: string;
  /** The planned first day, or null when it has not been decided. */
  startsOn: string | null;
  /** The planned last day, or null when it has not been decided. */
  endsOn: string | null;
}

/** A destination on the main route, with the stops within it. */
export interface TripDestinationValue extends TripStopValue {
  stops?: readonly TripStopValue[];
}

/**
 * What is wrong with a route, and where.
 *
 * `index` is always the destination. `stopIndex` is present when the problem
 * is a stop within that destination; it is absent when the destination itself
 * is at fault.
 */
export interface TripScheduleProblem {
  /** The message shown to the reader. */
  message: string;
  /** The destination that is at fault, or that holds the stop at fault. */
  index: number;
  /** The stop within that destination that is at fault, if any. */
  stopIndex?: number;
  /** The field of that place that is wrong. */
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
 * Validates the destinations of a trip as a whole.
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
 * Validates the stops of one destination.
 *
 * A destination may hold no stops at all. What it may not do is hold too many,
 * or hold one with no name.
 */
export function validateTripStops(names: readonly string[]): string | null {
  if (names.length > TRIP_MAX_STOPS_PER_DESTINATION) {
    return `A destination cannot have more than ${TRIP_MAX_STOPS_PER_DESTINATION} stops.`;
  }

  for (const name of names) {
    if (typeof name !== 'string' || name.trim().length === 0) {
      return 'The trip stop name is required.';
    }
  }

  return null;
}

/**
 * Validates the schedule of a route against the trip it belongs to.
 *
 * Every date must fall inside the trip's own dates, a destination may not end
 * before it starts, and the destinations must be able to be visited in the
 * order they are given. A stop must fall inside its destination's dates when
 * the destination has them, and inside the trip everywhere else. Gaps between
 * destinations are fine: the days that belong to no destination are the
 * traveller's own.
 *
 * This mirrors the domain rules (Tabiji.Domain.Trips.TripSchedule) step for
 * step, and says the same thing in the same words, so that the message the
 * reader sees here is the message the API would have given.
 *
 * Returns null when the route is sound.
 */
export function validateTripSchedule(
  tripStartsOn: string,
  tripEndsOn: string,
  destinations: readonly TripDestinationValue[],
): TripScheduleProblem | null {
  if (!isIsoDate(tripStartsOn) || !isIsoDate(tripEndsOn)) {
    return null;
  }

  for (let index = 0; index < destinations.length; index += 1) {
    const destination = destinations[index];

    const outside = outsideTrip(destination, tripStartsOn, tripEndsOn, index);

    if (outside !== null) {
      return outside;
    }

    const backwards = reversed(destination, index);

    if (backwards !== null) {
      return backwards;
    }

    const stops = destination.stops ?? [];

    for (let stopIndex = 0; stopIndex < stops.length; stopIndex += 1) {
      const stopProblem = stopWithinDestination(
        destination,
        stops[stopIndex],
        index,
        stopIndex,
        tripStartsOn,
        tripEndsOn,
      );

      if (stopProblem !== null) {
        return stopProblem;
      }
    }
  }

  return outOfOrder(destinations, tripStartsOn);
}

/**
 * Checks that a destination's dates fall inside the trip's own dates.
 */
function outsideTrip(
  destination: TripStopValue,
  tripStartsOn: string,
  tripEndsOn: string,
  index: number,
): TripScheduleProblem | null {
  const startsOn = destination.startsOn;
  const endsOn = destination.endsOn;

  if (startsOn !== null && isIsoDate(startsOn)) {
    if (startsOn < tripStartsOn) {
      return {
        index,
        field: 'startsOn',
        message: `"${destination.name}" starts on ${format(startsOn)}, which is before the trip starts on ${format(tripStartsOn)}.`,
      };
    }

    if (startsOn > tripEndsOn) {
      return {
        index,
        field: 'startsOn',
        message: `"${destination.name}" starts on ${format(startsOn)}, which is after the trip ends on ${format(tripEndsOn)}.`,
      };
    }
  }

  if (endsOn !== null && isIsoDate(endsOn)) {
    if (endsOn < tripStartsOn) {
      return {
        index,
        field: 'endsOn',
        message: `"${destination.name}" ends on ${format(endsOn)}, which is before the trip starts on ${format(tripStartsOn)}.`,
      };
    }

    if (endsOn > tripEndsOn) {
      return {
        index,
        field: 'endsOn',
        message: `"${destination.name}" ends on ${format(endsOn)}, which is after the trip ends on ${format(tripEndsOn)}.`,
      };
    }
  }

  return null;
}

/**
 * Checks that a destination does not end before it starts.
 */
function reversed(destination: TripStopValue, index: number): TripScheduleProblem | null {
  const startsOn = destination.startsOn;
  const endsOn = destination.endsOn;

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
      message: `"${destination.name}" ends on ${format(endsOn)} and starts on ${format(startsOn)}.`,
    };
  }

  return null;
}

/**
 * Checks one stop against the destination it belongs to.
 *
 * The bounds are the destination's own dates where it has them, and the trip's
 * dates everywhere else. A stop is a part of the place, so it can never run
 * outside the days the traveller is there.
 */
function stopWithinDestination(
  destination: TripDestinationValue,
  stop: TripStopValue,
  index: number,
  stopIndex: number,
  tripStartsOn: string,
  tripEndsOn: string,
): TripScheduleProblem | null {
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
      stopIndex,
      field: 'endsOn',
      message: `"${stop.name}", a stop at "${destination.name}", ends on ${format(endsOn)} and starts on ${format(startsOn)}.`,
    };
  }

  const lower = destination.startsOn ?? tripStartsOn;
  const upper = destination.endsOn ?? tripEndsOn;

  if (startsOn !== null && isIsoDate(startsOn)) {
    if (startsOn < lower) {
      return {
        index,
        stopIndex,
        field: 'startsOn',
        message:
          destination.startsOn !== null
            ? `"${stop.name}", a stop at "${destination.name}", starts on ${format(startsOn)}, which is before "${destination.name}" starts on ${format(lower)}.`
            : `"${stop.name}", a stop at "${destination.name}", starts on ${format(startsOn)}, which is before the trip starts on ${format(lower)}.`,
      };
    }

    if (startsOn > upper) {
      return {
        index,
        stopIndex,
        field: 'startsOn',
        message:
          destination.endsOn !== null
            ? `"${stop.name}", a stop at "${destination.name}", starts on ${format(startsOn)}, which is after "${destination.name}" ends on ${format(upper)}.`
            : `"${stop.name}", a stop at "${destination.name}", starts on ${format(startsOn)}, which is after the trip ends on ${format(upper)}.`,
      };
    }
  }

  if (endsOn !== null && isIsoDate(endsOn)) {
    if (endsOn < lower) {
      return {
        index,
        stopIndex,
        field: 'endsOn',
        message:
          destination.startsOn !== null
            ? `"${stop.name}", a stop at "${destination.name}", ends on ${format(endsOn)}, which is before "${destination.name}" starts on ${format(lower)}.`
            : `"${stop.name}", a stop at "${destination.name}", ends on ${format(endsOn)}, which is before the trip starts on ${format(lower)}.`,
      };
    }

    if (endsOn > upper) {
      return {
        index,
        stopIndex,
        field: 'endsOn',
        message:
          destination.endsOn !== null
            ? `"${stop.name}", a stop at "${destination.name}", ends on ${format(endsOn)}, which is after "${destination.name}" ends on ${format(upper)}.`
            : `"${stop.name}", a stop at "${destination.name}", ends on ${format(endsOn)}, which is after the trip ends on ${format(upper)}.`,
      };
    }
  }

  return null;
}

/**
 * Walks the main route in order and keeps a floor: the earliest day the next
 * destination may begin.
 *
 * Only destinations take part. A stop is a part of the place, not a leg of the
 * journey, so it never establishes a floor and never contradicts the order.
 */
function outOfOrder(
  destinations: readonly TripDestinationValue[],
  tripStartsOn: string,
): TripScheduleProblem | null {
  let floor = tripStartsOn;
  let anchor: TripStopValue | null = null;

  for (let index = 0; index < destinations.length; index += 1) {
    const destination = destinations[index];
    const startsOn =
      destination.startsOn !== null && isIsoDate(destination.startsOn)
        ? destination.startsOn
        : null;
    const endsOn =
      destination.endsOn !== null && isIsoDate(destination.endsOn)
        ? destination.endsOn
        : null;

    if (startsOn !== null) {
      if (startsOn < floor) {
        return {
          index,
          field: 'startsOn',
          message: contradiction(destination, 'starts', startsOn, anchor, floor),
        };
      }

      floor = endsOn ?? startsOn;
    } else if (endsOn !== null) {
      if (endsOn < floor) {
        return {
          index,
          field: 'endsOn',
          message: contradiction(destination, 'ends', endsOn, anchor, floor),
        };
      }

      floor = endsOn;
    } else {
      // A destination with no dates says nothing about when the journey
      // reaches it, so it changes nothing.
      continue;
    }

    anchor = destination;
  }

  return null;
}

/**
 * The message for a destination that contradicts the route it is on. It names
 * the destination at fault and the destination it conflicts with, so the
 * traveller can put it right without guessing.
 */
function contradiction(
  destination: TripStopValue,
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

  return `"${destination.name}" ${verb} on ${format(date)}, but ${conflict}.`;
}

/**
 * Group level validator for the array of destinations. The message is exposed
 * as `errors.tripDestinations.message` so the form can show it.
 *
 * The destinations in the array are groups, so the names are read out of them.
 */
export const tripDestinationsValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const destinations = control.value;

  if (!Array.isArray(destinations)) {
    return null;
  }

  const names = destinations.map((destination) => {
    if (typeof destination === 'string') {
      return destination;
    }

    const name = (destination as { name?: unknown } | null)?.name;

    return typeof name === 'string' ? name : '';
  });

  const message = validateTripDestinations(names);

  return message ? { tripDestinations: { message } } : null;
};

/**
 * Group level validator for one destination's stops. The message is exposed
 * as `errors.tripStops.message`.
 */
export const tripStopsValidator: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const stops = control.value;

  if (!Array.isArray(stops)) {
    return null;
  }

  const names = stops.map((stop) => {
    const name = (stop as { name?: unknown } | null)?.name;

    return typeof name === 'string' ? name : '';
  });

  const message = validateTripStops(names);

  return message ? { tripStops: { message } } : null;
};

/**
 * Group level validator for the whole form, checking the schedule of the
 * route against the trip's own dates.
 *
 * It sits on the form rather than on a place because it needs the trip's dates
 * and the whole route. The error is exposed as `errors.tripSchedule` and
 * carries the destination (and stop, where there is one) that is at fault, so
 * the message can be shown against it.
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
    destinations as TripDestinationValue[],
  );

  return problem
    ? {
        tripSchedule: {
          message: problem.message,
          index: problem.index,
          stopIndex: problem.stopIndex,
          field: problem.field,
        },
      }
    : null;
};

/**
 * Rejects a place that is made of nothing but spaces.
 *
 * The built in required validator only rejects an empty value, so a place of
 * spaces would pass it and only be caught by the route as a whole. This marks
 * the place itself as wrong.
 */
export const nonBlankValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;

  if (typeof value !== 'string' || value.length === 0) {
    return null;
  }

  return value.trim().length === 0 ? { blank: true } : null;
};

/**
 * Rejects a place whose end date is before its start date.
 *
 * This is the one rule about a place that the place can answer on its own.
 * The rest of the schedule is checked against the trip and its destination as
 * a whole.
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
