import { FormControl, FormGroup } from '@angular/forms';
import { tripDateRangeValidator, validateTripDates } from './trip-date-range';

describe('validateTripDates', () => {
  it('should accept an end date after the start date', () => {
    expect(validateTripDates('2027-04-01', '2027-04-14')).toBeNull();
  });

  it('should accept a single day trip', () => {
    expect(validateTripDates('2027-04-01', '2027-04-01')).toBeNull();
  });

  it('should reject an end date before the start date', () => {
    expect(validateTripDates('2027-04-14', '2027-04-01')).toBe(
      'The end date must be on or after the start date.',
    );
  });

  it('should accept a trip that crosses a year boundary', () => {
    expect(validateTripDates('2026-12-28', '2027-01-04')).toBeNull();
  });

  it('should ignore empty dates so that the required validator can report them', () => {
    expect(validateTripDates('', '')).toBeNull();
    expect(validateTripDates('2027-04-01', '')).toBeNull();
  });

  it('should ignore values that are not ISO dates', () => {
    expect(validateTripDates('not-a-date', '2027-04-01')).toBeNull();
  });
});

describe('tripDateRangeValidator', () => {
  function buildForm(startsOn: string, endsOn: string) {
    return new FormGroup({
      startsOn: new FormControl(startsOn),
      endsOn: new FormControl(endsOn),
    });
  }

  it('should return no error when the dates are in order', () => {
    const form = buildForm('2027-04-01', '2027-04-14');

    expect(tripDateRangeValidator(form)).toBeNull();
  });

  it('should return a message when the dates are out of order', () => {
    const form = buildForm('2027-04-14', '2027-04-01');

    expect(tripDateRangeValidator(form)).toEqual({
      tripDates: { message: 'The end date must be on or after the start date.' },
    });
  });
});
