/**
 * Trip date formatting.
 *
 * The API sends `DateOnly` values as plain `YYYY-MM-DD` strings. They are
 * parsed as UTC and formatted in UTC, so a trip can never appear to start a
 * day earlier or later because of the reader's timezone. This mirrors the
 * discipline already used by the date range rule in the trips feature.
 */

const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;
const MILLISECONDS_PER_DAY = 86_400_000;

/** The formatted shape of a trip's dates. */
export interface TripDateRange {
  /** The start date, for example "1 Apr". */
  readonly start: string;
  /** The end date, for example "14 Apr 2027". */
  readonly end: string;
  /** The whole range as one string, for example "1 Apr – 14 Apr 2027". */
  readonly label: string;
  /** The length of the trip in days, counting both ends. */
  readonly days: number;
  /** The length of the trip in words, for example "14 days". */
  readonly duration: string;
}

const dayMonth = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  timeZone: 'UTC',
});

const dayMonthYear = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
});

/** Parses `YYYY-MM-DD` as a UTC date, or returns null when it is not one. */
function parseIsoDate(value: string): Date | null {
  const match = ISO_DATE.exec(value);

  if (!match) {
    return null;
  }

  const [, year, month, day] = match;
  const date = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));

  return Number.isNaN(date.getTime()) ? null : date;
}

/**
 * Formats a single `YYYY-MM-DD` date for display, as "1 Apr 2027".
 *
 * Returns `null` when the value is not a plain date, so the caller can fall
 * back to showing the raw string rather than inventing a date.
 */
export function formatTripDate(value: string): string | null {
  const date = parseIsoDate(value);

  return date === null ? null : dayMonthYear.format(date);
}

/**
 * Formats a trip's start and end dates for display.
 *
 * Returns `null` when either date is not a plain `YYYY-MM-DD` value, so the
 * caller can fall back to showing the raw string rather than inventing a date.
 */
export function formatTripDates(startsOn: string, endsOn: string): TripDateRange | null {
  const start = parseIsoDate(startsOn);
  const end = parseIsoDate(endsOn);

  if (start === null || end === null) {
    return null;
  }

  const sameYear = start.getUTCFullYear() === end.getUTCFullYear();
  const startText = sameYear ? dayMonth.format(start) : dayMonthYear.format(start);
  const endText = dayMonthYear.format(end);

  const days = Math.round((end.getTime() - start.getTime()) / MILLISECONDS_PER_DAY) + 1;

  return {
    start: startText,
    end: endText,
    label: `${startText} – ${endText}`,
    days,
    duration: `${days} ${days === 1 ? 'day' : 'days'}`,
  };
}

/**
 * Formats the planned stay at a stop.
 *
 * A stop may have both dates, one of them or neither. The two together read
 * as a stay, one alone as a day that is fixed and one that is not.
 *
 * Returns `null` when neither date has been decided, or when a date is not a
 * plain `YYYY-MM-DD` value.
 */
export function formatDestinationDates(
  startsOn: string | null,
  endsOn: string | null,
): string | null {
  if (startsOn === null && endsOn === null) {
    return null;
  }

  if (startsOn !== null && endsOn !== null) {
    const start = parseIsoDate(startsOn);
    const end = parseIsoDate(endsOn);

    if (start === null || end === null) {
      return null;
    }

    const sameYear = start.getUTCFullYear() === end.getUTCFullYear();
    const startText = sameYear ? dayMonth.format(start) : dayMonthYear.format(start);

    return `${startText} – ${dayMonthYear.format(end)}`;
  }

  const single = parseIsoDate(startsOn ?? endsOn ?? '');

  if (single === null) {
    return null;
  }

  return startsOn !== null
    ? `From ${dayMonthYear.format(single)}`
    : `Until ${dayMonthYear.format(single)}`;
}
