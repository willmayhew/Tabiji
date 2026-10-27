import { formatPlanDay, formatTripDates } from './trip-dates';

describe('formatTripDates', () => {
  it('should format a trip that starts and ends in the same month', () => {
    const range = formatTripDates('2027-04-01', '2027-04-14');

    expect(range?.label).toBe('1 Apr – 14 Apr 2027');
    expect(range?.start).toBe('1 Apr');
    expect(range?.end).toBe('14 Apr 2027');
  });

  it('should format a trip that crosses a month', () => {
    const range = formatTripDates('2027-04-28', '2027-05-04');

    expect(range?.label).toBe('28 Apr – 4 May 2027');
  });

  it('should format a trip that crosses a year', () => {
    const range = formatTripDates('2026-12-28', '2027-01-04');

    expect(range?.label).toBe('28 Dec 2026 – 4 Jan 2027');
  });

  it('should count the days inclusively', () => {
    expect(formatTripDates('2027-04-01', '2027-04-14')?.days).toBe(14);
  });

  it('should count a single day trip as one day', () => {
    const range = formatTripDates('2027-04-01', '2027-04-01');

    expect(range?.days).toBe(1);
    expect(range?.duration).toBe('1 day');
    expect(range?.label).toBe('1 Apr – 1 Apr 2027');
  });

  it('should describe the length in words', () => {
    expect(formatTripDates('2027-04-01', '2027-04-14')?.duration).toBe('14 days');
  });

  it('should not shift a date across a day boundary', () => {
    // A naive local-time parse would move these to the previous day west of
    // UTC, or the next day east of it.
    const range = formatTripDates('2027-01-01', '2027-01-01');

    expect(range?.start).toBe('1 Jan');
    expect(range?.days).toBe(1);
  });

  it('should return null for a value that is not an ISO date', () => {
    expect(formatTripDates('not-a-date', '2027-04-14')).toBeNull();
    expect(formatTripDates('2027-04-01', 'not-a-date')).toBeNull();
    expect(formatTripDates('', '')).toBeNull();
  });
});

describe('formatPlanDay', () => {
  it('should lead with the day of the week', () => {
    // 2 April 2027 is a Friday.
    expect(formatPlanDay('2027-04-02')).toBe('Fri 2 Apr 2027');
  });

  it('should carry the year, so a stranded day is not mistaken for one nearby', () => {
    expect(formatPlanDay('2027-04-02')).toContain('2027');
  });

  it('should not shift a day across a day boundary', () => {
    // A naive local-time parse would name the previous day west of UTC, or
    // the next one east of it.
    expect(formatPlanDay('2027-01-01')).toBe('Fri 1 Jan 2027');
  });

  it('should return null for a value that is not an ISO date', () => {
    expect(formatPlanDay('not-a-date')).toBeNull();
    expect(formatPlanDay('')).toBeNull();
  });
});
