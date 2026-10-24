import { SavedPlace } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import { groupPlacesByStop } from './place-groups';

function stop(id: string, name: string, sortOrder = 0): TripDestination {
  return { id, name, sortOrder, startsOn: null, endsOn: null, latitude: null, longitude: null };
}

function place(id: string, destinationId: string | null): SavedPlace {
  return {
    id,
    destinationId,
    name: id,
    latitude: 0,
    longitude: 0,
    category: null,
    address: null,
    notes: null,
    createdAt: '2026-10-20T09:00:00+00:00',
  };
}

/** Every place id, in the order the groups present them. */
function ids(groups: ReturnType<typeof groupPlacesByStop>): string[] {
  return groups.flatMap((group) => group.places.map((saved) => saved.id));
}

describe('groupPlacesByStop', () => {
  const route = [stop('a', 'Kyoto', 0), stop('b', 'Nara', 1), stop('c', 'Osaka', 2)];

  it('should group the places under the stop they are filed to, in route order', () => {
    const groups = groupPlacesByStop(route, [
      place('osaka-one', 'c'),
      place('kyoto-one', 'a'),
    ]);

    expect(groups.map((group) => group.destination?.id)).toEqual(['a', 'c']);
  });

  it('should keep the order the places were given within a group', () => {
    const groups = groupPlacesByStop(route, [
      place('first', 'a'),
      place('second', 'b'),
      place('third', 'a'),
    ]);

    const kyoto = groups.find((group) => group.destination?.id === 'a');

    expect(kyoto?.places.map((saved) => saved.id)).toEqual(['first', 'third']);
  });

  it('should put the places filed under no stop last, in a group of their own', () => {
    const groups = groupPlacesByStop(route, [
      place('loose-one', null),
      place('kyoto-one', 'a'),
      place('loose-two', null),
    ]);

    expect(groups).toHaveLength(2);
    expect(groups[0].destination?.id).toBe('a');
    expect(groups[1].destination).toBeNull();
    expect(groups[1].places.map((saved) => saved.id)).toEqual(['loose-one', 'loose-two']);
  });

  it('should treat a place filed under a stop that is not in the trip as unfiled', () => {
    const groups = groupPlacesByStop(route, [place('stray', 'gone')]);

    expect(groups).toHaveLength(1);
    expect(groups[0].destination).toBeNull();
    expect(groups[0].places.map((saved) => saved.id)).toEqual(['stray']);
  });

  it('should leave out a stop with nothing filed under it', () => {
    const groups = groupPlacesByStop(route, [place('kyoto-one', 'a'), place('osaka-one', 'c')]);

    expect(groups.map((group) => group.destination?.name)).toEqual(['Kyoto', 'Osaka']);
  });

  it('should leave out the unfiled group when every place is filed', () => {
    const groups = groupPlacesByStop(route, [place('kyoto-one', 'a')]);

    expect(groups.map((group) => group.destination)).toHaveLength(1);
    expect(groups[0].destination).not.toBeNull();
  });

  it('should group everything as unfiled when the trip has no stops', () => {
    const groups = groupPlacesByStop([], [place('one', null), place('two', 'gone')]);

    expect(groups).toHaveLength(1);
    expect(groups[0].destination).toBeNull();
    expect(ids(groups)).toEqual(['one', 'two']);
  });

  it('should return nothing when nothing is saved', () => {
    expect(groupPlacesByStop(route, [])).toEqual([]);
  });

  it('should keep two stops with the same name apart', () => {
    const twice = [stop('a', 'Rome', 0), stop('b', 'Rome', 1)];

    const groups = groupPlacesByStop(twice, [place('one', 'a'), place('two', 'b')]);

    expect(groups).toHaveLength(2);
    expect(groups[0].places.map((saved) => saved.id)).toEqual(['one']);
    expect(groups[1].places.map((saved) => saved.id)).toEqual(['two']);
  });

  it('should put every place in exactly one group', () => {
    const saved = [
      place('one', 'a'),
      place('two', 'b'),
      place('three', null),
      place('four', 'gone'),
      place('five', 'c'),
      place('six', 'a'),
    ];

    const grouped = ids(groupPlacesByStop(route, saved));

    expect(grouped).toHaveLength(saved.length);
    expect([...grouped].sort()).toEqual(saved.map((entry) => entry.id).sort());
    expect(new Set(grouped).size).toBe(saved.length);
  });
});
