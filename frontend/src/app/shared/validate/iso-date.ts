/**
 * Plain `YYYY-MM-DD` dates.
 *
 * The API sends `DateOnly` values in this shape and they are compared as
 * text: `YYYY-MM-DD` sorts lexicographically exactly as it sorts
 * chronologically, so no timezone and no locale can shift the result.
 */

const ISO_DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;

/** Whether the value is a plain date of that shape. */
export function isIsoDate(value: string): boolean {
  return ISO_DATE_PATTERN.test(value);
}

/**
 * The date as a UTC timestamp, or null when it is not a plain date.
 *
 * Used where a date has to be turned into a number, such as counting the
 * days between two of them.
 */
export function toUtcTimestamp(isoDate: string): number | null {
  const match = ISO_DATE_PATTERN.exec(isoDate);

  if (!match) {
    return null;
  }

  const [, year, month, day] = match;

  return Date.UTC(Number(year), Number(month) - 1, Number(day));
}
