import { TripDestination, TripStop } from '../../core/models/trip';
import { groupStopsByDay } from './plan-days';

const startsOn = '2027-04-01';
const endsOn = '2027-04-10';

function stop(overrides: Partial<TripStop> & Pick<TripStop, 'id' | 'name'>): TripStop {
  return {
    sortOrder: 0,
    startsOn: null,
    endsOn: null,
    latitude: null,
    longitude: null,
    ...overrides,
  };
}

const gion = stop({
  id: 'gion',
  name: 'Gion',
  sortOrder: 0,
  startsOn: '2027-04-02',
  endsOn: '2027-04-03',
  plannedOn: '2027-04-02',
});
const arashiyama = stop({
  id: 'arashiyama',
  name: 'Arashiyama',
  sortOrder: 1,
  plannedOn: '2027-04-02',
});
const unfiled = stop({ id: 'unfiled', name: 'Someday', plannedOn: '2027-04-02' });
const unplanned = stop({ id: 'unplanned', name: 'One day' });

const kyoto: TripDestination = {
  id: 'kyoto',
  name: 'Kyoto',
  sortOrder: 0,
  startsOn: '2027-04-01',
  endsOn: '2027-04-04',
  latitude: null,
  longitude: null,
  stops: [gion, arashiyama, unplanned],
};

const tokyo: TripDestination = {
  id: 'tokyo',
  name: 'Tokyo',
  sortOrder: 1,
  startsOn: '2027-04-05',
  endsOn: '2027-04-08',
  latitude: null,
  longitude: null,
  stops: [],
};

function planFor(
  destinations: readonly TripDestination[],
  unfiledPlaces: readonly TripStop[] = [],
) {
  return groupStopsByDay(startsOn, endsOn, destinations, unfiledPlaces);
}

describe('groupStopsByDay', () => {
  it('groups the places by the day they are planned for, in calendar order', () => {
    const plan = planFor([kyoto, tokyo], [unfiled]);

    expect(plan.planned.map((day) => day.date)).toEqual(['2027-04-02']);
    expect(plan.planned[0].places.map((place) => place.id)).toEqual([
      'gion',
      'arashiyama',
      'unfiled',
    ]);
  });

  it('keeps the route order within a day, with the unfiled places last', () => {
    const plan = planFor([kyoto, tokyo], [unfiled]);

    expect(plan.planned[0].places.map((place) => place.name)).toEqual([
      'Gion',
      'Arashiyama',
      'Someday',
    ]);
  });

  it('names the stop whose stay covers the day, before its destination', () => {
    const plan = planFor([kyoto, tokyo], [unfiled]);

    expect(plan.planned[0].stop).toEqual({
      kind: 'stop',
      id: 'gion',
      name: 'Gion',
      parentName: 'Kyoto',
    });
  });

  it('falls back to the destination on a day no stop covers', () => {
    const day = stop({ id: 'day', name: 'A day', plannedOn: '2027-04-06' });
    const plan = planFor([kyoto, tokyo], [day]);

    const group = plan.planned.find((candidate) => candidate.date === '2027-04-06');

    expect(group?.stop).toEqual({
      kind: 'destination',
      id: 'tokyo',
      name: 'Tokyo',
      parentName: null,
    });
  });

  it('includes both ends of a stay and the day after it in the destination', () => {
    const last = stop({ id: 'last', name: 'Last', plannedOn: '2027-04-03' });
    const after = stop({ id: 'after', name: 'After', plannedOn: '2027-04-04' });
    const plan = planFor([kyoto, tokyo], [last, after]);

    expect(plan.planned.find((day) => day.date === '2027-04-03')?.stop?.name).toBe('Gion');
    expect(plan.planned.find((day) => day.date === '2027-04-04')?.stop?.name).toBe('Kyoto');
  });

  it('does not let a stop with one date cover a day', () => {
    const oneDate = stop({
      id: 'one',
      name: 'One date',
      startsOn: '2027-04-02',
      plannedOn: '2027-04-02',
    });
    const plan = groupStopsByDay(startsOn, endsOn, [
      { ...kyoto, stops: [oneDate] },
    ], []);

    expect(plan.planned[0].stop?.name).toBe('Kyoto');
  });

  it('keeps places planned outside the trip apart', () => {
    const outside = stop({ id: 'outside', name: 'Outside', plannedOn: '2027-04-12' });
    const plan = planFor(
      [
        { ...kyoto, stops: [] },
        { ...tokyo, stops: [] },
      ],
      [outside],
    );

    expect(plan.planned).toEqual([]);
    expect(plan.outside.map((day) => day.date)).toEqual(['2027-04-12']);
    expect(plan.outside[0].places.map((place) => place.id)).toEqual(['outside']);
  });

  it('keeps places with no day unplanned', () => {
    const plan = planFor([kyoto, tokyo], [unfiled]);

    expect(plan.unplanned.map((place) => place.id)).toEqual(['unplanned']);
  });

  it('never loses a place', () => {
    const plan = planFor([kyoto, tokyo], [unfiled, unplanned]);
    const ids = [
      ...plan.planned.flatMap((day) => day.places.map((place) => place.id)),
      ...plan.outside.flatMap((day) => day.places.map((place) => place.id)),
      ...plan.unplanned.map((place) => place.id),
    ];

    expect(new Set(ids)).toEqual(
      new Set(['gion', 'arashiyama', 'unplanned', 'unfiled']),
    );
  });
});
