import { LocatedPoint, markerOffsets } from './marker-offsets';

/** A marker is twenty-four pixels across, so this is the room one needs. */
const MARKER_WIDTH = 24;

function point(id: string, latitude: number, longitude: number): LocatedPoint {
  return { id, latitude, longitude };
}

/** The distance between two offsets, which is how far apart two markers sit. */
function distance(a: { x: number; y: number }, b: { x: number; y: number }): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function distanceFromOrigin(offset: { x: number; y: number }): number {
  return Math.hypot(offset.x, offset.y);
}

/** The closest any two of a set of offsets come to each other. */
function closestPair(offsets: readonly { x: number; y: number }[]): number {
  let closest = Number.POSITIVE_INFINITY;

  for (let left = 0; left < offsets.length; left += 1) {
    for (let right = left + 1; right < offsets.length; right += 1) {
      closest = Math.min(closest, distance(offsets[left], offsets[right]));
    }
  }

  return closest;
}

describe('markerOffsets', () => {
  it('should leave a marker that shares its place with nobody exactly where it belongs', () => {
    const offsets = markerOffsets([point('a', 34.9671, 135.7727)]);

    expect(offsets.size).toBe(0);
  });

  it('should leave markers at different places exactly where they belong', () => {
    const offsets = markerOffsets([
      point('a', 34.9671, 135.7727),
      point('b', 35.0116, 135.7681),
      point('c', 64.1466, -21.9426),
    ]);

    expect(offsets.size).toBe(0);
  });

  it('should push two markers at the same place apart', () => {
    const offsets = markerOffsets([
      point('a', 34.9671, 135.7727),
      point('b', 34.9671, 135.7727),
    ]);

    const first = offsets.get('a');
    const second = offsets.get('b');

    expect(first).toBeDefined();
    expect(second).toBeDefined();
    expect(distance(first!, second!)).toBeGreaterThanOrEqual(MARKER_WIDTH);

    // One above the place and one below it, so the spread is centred on it.
    expect(first!.x).toBe(0);
    expect(first!.y).toBe(-15);
    expect(second!.x).toBe(0);
    expect(second!.y).toBe(15);
  });

  it('should spread a group round the ring in the order the points were given', () => {
    const offsets = markerOffsets([
      point('a', 1.0, 2.0),
      point('b', 1.0, 2.0),
      point('c', 1.0, 2.0),
    ]);

    // The first sits at the top of the ring, and the rest follow clockwise.
    expect(offsets.get('a')!.x).toBe(0);
    expect(offsets.get('a')!.y).toBeLessThan(0);
    expect(offsets.get('b')!.x).toBeGreaterThan(0);
    expect(offsets.get('b')!.y).toBeGreaterThan(0);
    expect(offsets.get('c')!.x).toBeLessThan(0);
    expect(offsets.get('c')!.y).toBeGreaterThan(0);
  });

  it('should spread only the markers that share a place', () => {
    const offsets = markerOffsets([
      point('alone', 64.1466, -21.9426),
      point('a', 1.0, 2.0),
      point('b', 1.0, 2.0),
    ]);

    expect(offsets.size).toBe(2);
    expect(offsets.has('alone')).toBe(false);
    expect(offsets.has('a')).toBe(true);
    expect(offsets.has('b')).toBe(true);
  });

  it('should tell apart two places that are near each other but not the same', () => {
    const offsets = markerOffsets([
      point('a', 34.9671, 135.7727),
      point('b', 34.9672, 135.7727),
    ]);

    expect(offsets.size).toBe(0);
  });

  it('should never let markers at the same place cover each other, however many there are', () => {
    for (let count = 2; count <= 12; count += 1) {
      const points = Array.from({ length: count }, (_, index) =>
        point(`p${index}`, 1.0, 2.0),
      );

      const offsets = [...markerOffsets(points).values()];

      expect(offsets).toHaveLength(count);
      expect(closestPair(offsets)).toBeGreaterThanOrEqual(MARKER_WIDTH - 1);
    }
  });

  it('should never spread a crowded place further than a small ring', () => {
    const points = Array.from({ length: 40 }, (_, index) => point(`p${index}`, 1.0, 2.0));

    const offsets = [...markerOffsets(points).values()];
    const furthest = Math.max(...offsets.map(distanceFromOrigin));

    expect(furthest).toBeLessThanOrEqual(60);
  });

  it('should give the same offsets for the same points', () => {
    const points = [
      point('a', 1.0, 2.0),
      point('b', 1.0, 2.0),
      point('c', 1.0, 2.0),
      point('d', 1.0, 2.0),
    ];

    expect([...markerOffsets(points)]).toEqual([...markerOffsets(points)]);
  });

  it('should give nothing for no points', () => {
    expect(markerOffsets([]).size).toBe(0);
  });
});
