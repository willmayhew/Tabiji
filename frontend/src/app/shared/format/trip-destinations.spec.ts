import { TripDestination } from '../../core/models/trip';
import { firstDestination, routeLabel, routeSummary } from './trip-destinations';

const route: TripDestination[] = [
  { name: 'Rome', sortOrder: 0 },
  { name: 'Amalfi', sortOrder: 1 },
  { name: 'Naples', sortOrder: 2 },
];

describe('firstDestination', () => {
  it('should give the first stop', () => {
    expect(firstDestination(route)).toBe('Rome');
  });

  it('should give nothing when there are no stops', () => {
    expect(firstDestination([])).toBeNull();
  });
});

describe('routeLabel', () => {
  it('should join the stops with an arrow', () => {
    expect(routeLabel(route)).toBe('Rome → Amalfi → Naples');
  });

  it('should give one stop on its own', () => {
    expect(routeLabel([{ name: 'Rome', sortOrder: 0 }])).toBe('Rome');
  });

  it('should count the stops it leaves out', () => {
    const longer: TripDestination[] = [
      ...route,
      { name: 'Bari', sortOrder: 3 },
      { name: 'Matera', sortOrder: 4 },
    ];

    expect(routeLabel(longer)).toBe('Rome → Amalfi → Naples → +2 more');
  });

  it('should give nothing when there are no stops', () => {
    expect(routeLabel([])).toBe('');
  });
});

describe('routeSummary', () => {
  it('should count the stops in words', () => {
    expect(routeSummary(route)).toBe('3 stops');
  });

  it('should use the singular for one stop', () => {
    expect(routeSummary([{ name: 'Rome', sortOrder: 0 }])).toBe('1 stop');
  });

  it('should count no stops', () => {
    expect(routeSummary([])).toBe('0 stops');
  });
});
