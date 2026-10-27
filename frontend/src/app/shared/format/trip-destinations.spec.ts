import { TripDestination } from '../../core/models/trip';
import { routeLabel, routeSummary } from './trip-destinations';

const route: TripDestination[] = [
  { id: 'a1', name: 'Rome', sortOrder: 0, startsOn: null, endsOn: null, latitude: null, longitude: null },
  { id: 'b2', name: 'Amalfi', sortOrder: 1, startsOn: null, endsOn: null, latitude: null, longitude: null },
  { id: 'c3', name: 'Naples', sortOrder: 2, startsOn: null, endsOn: null, latitude: null, longitude: null },
];

describe('routeLabel', () => {
  it('should join the stops with an arrow', () => {
    expect(routeLabel(route)).toBe('Rome → Amalfi → Naples');
  });

  it('should give one stop on its own', () => {
    expect(routeLabel([{ id: 'a1', name: 'Rome', sortOrder: 0, startsOn: null, endsOn: null, latitude: null, longitude: null }])).toBe(
      'Rome',
    );
  });

  it('should count the stops it leaves out', () => {
    const longer: TripDestination[] = [
      ...route,
      { id: 'd4', name: 'Bari', sortOrder: 3, startsOn: null, endsOn: null, latitude: null, longitude: null },
      { id: 'e5', name: 'Matera', sortOrder: 4, startsOn: null, endsOn: null, latitude: null, longitude: null },
    ];

    expect(routeLabel(longer)).toBe('Rome → Amalfi → Naples → +2 more');
  });

  it('should give nothing when there are no stops', () => {
    expect(routeLabel([])).toBe('');
  });
});

describe('routeSummary', () => {
  it('should count the stops in words', () => {
    expect(routeSummary(route)).toBe('3 destinations');
  });

  it('should use the singular for one stop', () => {
    expect(
      routeSummary([{ id: 'a1', name: 'Rome', sortOrder: 0, startsOn: null, endsOn: null, latitude: null, longitude: null }]),
    ).toBe('1 destination');
  });

  it('should count no stops', () => {
    expect(routeSummary([])).toBe('0 destinations');
  });
});
