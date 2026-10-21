import { TRIP_MAX_DESTINATIONS } from '../../core/models/trip';
import {
  nonBlankValidator,
  tripDestinationsValidator,
  validateTripDestinations,
} from './trip-destinations';

describe('validateTripDestinations', () => {
  it('should accept a route with stops', () => {
    expect(validateTripDestinations(['Rome', 'Amalfi'])).toBeNull();
  });

  it('should reject an empty route', () => {
    expect(validateTripDestinations([])).toBe('A trip must have at least one destination.');
  });

  it('should reject a blank stop', () => {
    expect(validateTripDestinations(['Rome', '   '])).toBe('The trip destination name is required.');
  });

  it('should reject too many stops', () => {
    const stops = Array.from({ length: TRIP_MAX_DESTINATIONS + 1 }, (_, index) => `Stop ${index}`);

    expect(validateTripDestinations(stops)).toBe(
      `A trip cannot have more than ${TRIP_MAX_DESTINATIONS} destinations.`,
    );
  });

  it('should allow the same place twice', () => {
    expect(validateTripDestinations(['Rome', 'Florence', 'Rome'])).toBeNull();
  });
});

describe('tripDestinationsValidator', () => {
  it('should return no error when the route is acceptable', () => {
    expect(tripDestinationsValidator({ value: ['Rome'] } as never)).toBeNull();
  });

  it('should return a message when the route is empty', () => {
    const errors = tripDestinationsValidator({ value: [] } as never);

    expect(errors?.['tripDestinations']?.['message']).toBe(
      'A trip must have at least one destination.',
    );
  });

  it('should ignore a control whose value is not a list', () => {
    expect(tripDestinationsValidator({ value: null } as never)).toBeNull();
  });
});

describe('nonBlankValidator', () => {
  it('should accept a stop with a name', () => {
    expect(nonBlankValidator({ value: 'Rome' } as never)).toBeNull();
  });

  it('should reject a stop made of nothing but spaces', () => {
    expect(nonBlankValidator({ value: '   ' } as never)).not.toBeNull();
  });

  it('should leave an empty stop to the required validator', () => {
    expect(nonBlankValidator({ value: '' } as never)).toBeNull();
  });
});
