import { TRIP_MAX_DESTINATIONS } from '../../core/models/trip';
import {
  destinationDatesValidator,
  nonBlankValidator,
  tripDestinationsValidator,
  tripScheduleValidator,
  validateTripDestinations,
  validateTripSchedule,
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

describe('destinationDatesValidator', () => {
  function build(startsOn: string, endsOn: string) {
    return {
      get: (name: string) =>
        name === 'startsOn' ? { value: startsOn } : name === 'endsOn' ? { value: endsOn } : null,
    };
  }

  it('should accept dates that are in order', () => {
    expect(destinationDatesValidator(build('2027-04-01', '2027-04-05') as never)).toBeNull();
  });

  it('should reject an end date before the start date', () => {
    const errors = destinationDatesValidator(build('2027-04-05', '2027-04-01') as never);

    expect(errors?.['destinationDates']?.['message']).toBe(
      'The end date must be on or after the start date.',
    );
  });

  it('should leave a stop with only one date to the route as a whole', () => {
    expect(destinationDatesValidator(build('2027-04-01', '') as never)).toBeNull();
  });
});

describe('validateTripSchedule', () => {
  const tripStartsOn = '2027-05-16';
  const tripEndsOn = '2027-06-05';

  function stop(name: string, startsOn: string | null = null, endsOn: string | null = null) {
    return { name, startsOn, endsOn };
  }

  function check(...stops: ReturnType<typeof stop>[]) {
    return validateTripSchedule(tripStartsOn, tripEndsOn, stops);
  }

  it('should accept a route with no dates at all', () => {
    expect(check(stop('Osaka'), stop('Kyoto'), stop('Ise'))).toBeNull();
  });

  it('should accept fully dated stops in order', () => {
    expect(
      check(
        stop('Osaka', '2027-05-16', '2027-05-20'),
        stop('Kyoto', '2027-05-20', '2027-05-24'),
        stop('Tokyo', '2027-05-26', '2027-06-05'),
      ),
    ).toBeNull();
  });

  it('should refuse a stop that ends before it starts', () => {
    const problem = check(stop('Kyoto', '2027-05-20', '2027-05-18'));

    expect(problem?.index).toBe(0);
    expect(problem?.message).toContain('Kyoto');
  });

  it('should refuse a date before the trip starts', () => {
    const problem = check(stop('Osaka', '2027-05-01'));

    expect(problem?.field).toBe('startsOn');
    expect(problem?.message).toContain('before the trip starts');
  });

  it('should refuse a date after the trip ends', () => {
    const problem = check(stop('Tokyo', null, '2027-06-10'));

    expect(problem?.field).toBe('endsOn');
    expect(problem?.message).toContain('after the trip ends');
  });

  it('should refuse two stays that overlap', () => {
    const problem = check(
      stop('Osaka', '2027-05-16', '2027-05-20'),
      stop('Kyoto', '2027-05-18', '2027-05-22'),
    );

    expect(problem?.index).toBe(1);
    expect(problem?.message).toContain('Osaka');
    expect(problem?.message).toContain('Kyoto');
  });

  it('should accept a stop that begins on the day the last one ended', () => {
    expect(
      check(stop('Osaka', '2027-05-16', '2027-05-20'), stop('Kyoto', '2027-05-20', '2027-05-24')),
    ).toBeNull();
  });

  it('should accept a stop with only a start after a full stop', () => {
    expect(
      check(stop('Osaka', '2027-05-16', '2027-05-20'), stop('Kyoto', '2027-05-20')),
    ).toBeNull();
  });

  it('should refuse a stop with only a start that begins inside the previous stay', () => {
    const problem = check(
      stop('Osaka', '2027-05-16', '2027-05-20'),
      stop('Kyoto', '2027-05-18'),
    );

    expect(problem?.index).toBe(1);
    expect(problem?.message).toContain('Osaka');
    expect(problem?.message).toContain('Kyoto');
  });

  it('should refuse a stop with only an end that ends too early', () => {
    const problem = check(
      stop('Osaka', '2027-05-16', '2027-05-20'),
      stop('Kyoto', null, '2027-05-18'),
    );

    expect(problem?.index).toBe(1);
    expect(problem?.message).toContain('Kyoto');
  });

  it('should accept a stop with only an end after a full stop', () => {
    expect(
      check(stop('Osaka', '2027-05-16', '2027-05-20'), stop('Kyoto', null, '2027-05-24')),
    ).toBeNull();
  });

  it('should refuse a start only stop before an earlier start only stop', () => {
    expect(check(stop('Osaka', '2027-05-20'), stop('Kyoto', '2027-05-18'))).not.toBeNull();
  });

  it('should refuse an end only stop before an earlier end only stop', () => {
    expect(check(stop('Osaka', null, '2027-05-20'), stop('Kyoto', null, '2027-05-18'))).not.toBeNull();
  });

  it('should accept a start only stop before an end only stop', () => {
    expect(check(stop('Osaka', '2027-05-16'), stop('Kyoto', null, '2027-05-20'))).toBeNull();
  });

  it('should refuse an end only stop before a start only stop', () => {
    expect(check(stop('Osaka', null, '2027-05-20'), stop('Kyoto', '2027-05-18'))).not.toBeNull();
  });

  it('should accept an undated stop between two dated stops', () => {
    expect(
      check(
        stop('Osaka', '2027-05-16', '2027-05-20'),
        stop('Ise'),
        stop('Tokyo', '2027-05-26', '2027-06-05'),
      ),
    ).toBeNull();
  });

  it('should not let an undated stop reset the floor', () => {
    const problem = check(
      stop('Osaka', '2027-05-16', '2027-05-20'),
      stop('Ise'),
      stop('Tokyo', '2027-05-18', '2027-06-05'),
    );

    expect(problem?.index).toBe(2);
    expect(problem?.message).toContain('Osaka');
    expect(problem?.message).toContain('Tokyo');
  });

  it('should accept a gap between stops', () => {
    expect(
      check(stop('Osaka', '2027-05-16', '2027-05-20'), stop('Tokyo', '2027-05-26', '2027-06-05')),
    ).toBeNull();
  });

  it('should refuse stops that are out of order and name both of them', () => {
    const problem = check(
      stop('Tokyo', '2027-05-20', '2027-06-05'),
      stop('Kyoto', '2027-05-16', '2027-05-24'),
    );

    expect(problem?.message).toContain('Tokyo');
    expect(problem?.message).toContain('Kyoto');
  });

  it('should accept the route from the brief', () => {
    expect(
      check(
        stop('Osaka', '2027-05-16', '2027-05-20'),
        stop('Kyoto', '2027-05-20', '2027-05-24'),
        stop('Ise'),
        stop('Tokyo', '2027-05-26', '2027-06-05'),
      ),
    ).toBeNull();
  });

  it('should ignore dates that are not plain dates', () => {
    expect(validateTripSchedule('not-a-date', tripEndsOn, [stop('Osaka')])).toBeNull();
  });
});

describe('tripScheduleValidator', () => {
  function build(startsOn: string, endsOn: string, destinations: unknown[]) {
    return {
      get: (name: string) => {
        if (name === 'startsOn') {
          return { value: startsOn };
        }

        if (name === 'endsOn') {
          return { value: endsOn };
        }

        if (name === 'destinations') {
          return { value: destinations };
        }

        return null;
      },
    };
  }

  it('should return no error when the route is sound', () => {
    const form = build('2027-05-16', '2027-06-05', [
      { name: 'Osaka', startsOn: '2027-05-16', endsOn: '2027-05-20' },
    ]);

    expect(tripScheduleValidator(form as never)).toBeNull();
  });

  it('should name the stop that is at fault', () => {
    const form = build('2027-05-16', '2027-06-05', [
      { name: 'Osaka', startsOn: '2027-05-16', endsOn: '2027-05-20' },
      { name: 'Kyoto', startsOn: '2027-05-18', endsOn: null },
    ]);

    const errors = tripScheduleValidator(form as never);

    expect(errors?.['tripSchedule']?.['index']).toBe(1);
    expect(errors?.['tripSchedule']?.['message']).toContain('Kyoto');
  });
});
