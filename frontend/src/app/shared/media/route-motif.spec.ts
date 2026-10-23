import { hashSeed, plateDimensions, plateTone, routeMotif } from './route-motif';

describe('hashSeed', () => {
  it('should give the same hash for the same seed', () => {
    expect(hashSeed('a-trip-id')).toBe(hashSeed('a-trip-id'));
  });

  it('should give different hashes for different seeds', () => {
    expect(hashSeed('a-trip-id')).not.toBe(hashSeed('another-trip-id'));
  });

  it('should give an unsigned 32 bit number', () => {
    const hash = hashSeed('a-trip-id');

    expect(Number.isInteger(hash)).toBe(true);
    expect(hash).toBeGreaterThanOrEqual(0);
    expect(hash).toBeLessThanOrEqual(0xffffffff);
  });
});

describe('routeMotif', () => {
  it('should draw the same route for the same seed', () => {
    expect(routeMotif('a-trip-id', 3)).toEqual(routeMotif('a-trip-id', 3));
  });

  it('should draw a different route for a different seed', () => {
    expect(routeMotif('a-trip-id', 3).path).not.toBe(routeMotif('another-trip-id', 3).path);
  });

  it('should draw one waypoint for each stop', () => {
    for (const stops of [2, 3, 4, 5, 6, 10, 20]) {
      expect(routeMotif('a-trip-id', stops).nodes).toHaveLength(stops);
    }
  });

  it('should draw two waypoints for a trip with a single stop', () => {
    expect(routeMotif('a-trip-id', 1).nodes).toHaveLength(2);
  });

  it('should draw two waypoints for a trip with no stops at all', () => {
    expect(routeMotif('a-trip-id', 0).nodes).toHaveLength(2);
  });

  it('should draw the same number of waypoints on a band as on a card', () => {
    for (const stops of [1, 2, 3, 6]) {
      expect(routeMotif('a-trip-id', stops, 'band').nodes).toHaveLength(
        routeMotif('a-trip-id', stops, 'card').nodes.length,
      );
    }
  });

  it('should start the path at the first waypoint', () => {
    const motif = routeMotif('a-trip-id', 3);
    const first = motif.nodes[0];

    expect(motif.path.startsWith(`M ${first.x} ${first.y}`)).toBe(true);
  });

  it('should draw the line through every waypoint', () => {
    const motif = routeMotif('a-trip-id', 6);

    for (const node of motif.nodes) {
      expect(motif.path).toContain(`${node.x} ${node.y}`);
    }
  });

  it('should draw a graticule of chart lines', () => {
    const graticule = routeMotif('a-trip-id', 3).graticule;

    expect(graticule.startsWith('M ')).toBe(true);
    expect(graticule.split('M ').length).toBe(8);
  });

  it('should keep every waypoint on the drawing', () => {
    const { width, height } = plateDimensions('card');

    for (const stops of [1, 2, 3, 6, 12, 20]) {
      for (const node of routeMotif('a-trip-id', stops).nodes) {
        expect(node.x).toBeGreaterThanOrEqual(0);
        expect(node.x).toBeLessThanOrEqual(width);
        expect(node.y).toBeGreaterThanOrEqual(0);
        expect(node.y).toBeLessThanOrEqual(height);
      }
    }
  });

  it('should never let the route double back on itself', () => {
    for (const stops of [2, 3, 6, 12, 20]) {
      const nodes = routeMotif('a-trip-id', stops).nodes;

      for (let index = 1; index < nodes.length; index += 1) {
        expect(nodes[index].x).toBeGreaterThan(nodes[index - 1].x);
      }
    }
  });

  it('should keep a long route on the drawing rather than piling up at one edge', () => {
    const { height } = plateDimensions('card');
    const nodes = routeMotif('a-trip-id', 20).nodes;
    const distinct = new Set(nodes.map((node) => node.y));

    expect(distinct.size).toBeGreaterThan(4);
    expect(Math.max(...nodes.map((node) => node.y))).toBeLessThanOrEqual(height);
  });

  it('should draw a wider band than a card', () => {
    expect(plateDimensions('band').width).toBeGreaterThan(plateDimensions('card').width);
  });

  it('should give the band the ratio of the two-column plate', () => {
    const { width, height } = plateDimensions('band');

    expect(width / height).toBeCloseTo(11 / 4);
  });

  it('should draw on an empty seed without throwing', () => {
    expect(() => routeMotif('', 3)).not.toThrow();
  });
});

describe('plateTone', () => {
  it('should give the same ground for the same seed', () => {
    expect(plateTone('a-trip-id')).toBe(plateTone('a-trip-id'));
  });

  it('should give one of the four grounds', () => {
    const grounds = ['paper', 'sage', 'clay', 'forest'];

    for (const seed of ['one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight']) {
      expect(grounds).toContain(plateTone(seed));
    }
  });

  it('should use all four grounds across a spread of trips', () => {
    const grounds = new Set<string>();

    for (let index = 0; index < 40; index += 1) {
      grounds.add(plateTone(`trip-${index}`));
    }

    expect(grounds.size).toBe(4);
  });
});
