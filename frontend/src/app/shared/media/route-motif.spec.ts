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
    expect(routeMotif('a-trip-id')).toEqual(routeMotif('a-trip-id'));
  });

  it('should draw a different route for a different seed', () => {
    expect(routeMotif('a-trip-id').path).not.toBe(routeMotif('another-trip-id').path);
  });

  it('should draw between three and five waypoints', () => {
    for (const seed of ['one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight']) {
      const nodes = routeMotif(seed).nodes;

      expect(nodes.length).toBeGreaterThanOrEqual(3);
      expect(nodes.length).toBeLessThanOrEqual(5);
    }
  });

  it('should draw one more waypoint on a band than on a card', () => {
    for (const seed of ['one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight']) {
      const band = routeMotif(seed, 'band').nodes;
      const card = routeMotif(seed, 'card').nodes;

      expect(band.length).toBe(card.length + 1);
      expect(band.length).toBeGreaterThanOrEqual(4);
      expect(band.length).toBeLessThanOrEqual(6);
    }
  });

  it('should start the path at the first waypoint', () => {
    const motif = routeMotif('a-trip-id');
    const first = motif.nodes[0];

    expect(motif.path.startsWith(`M ${first.x} ${first.y}`)).toBe(true);
  });

  it('should draw the line through every waypoint', () => {
    const motif = routeMotif('a-trip-id');

    for (const node of motif.nodes) {
      expect(motif.path).toContain(`${node.x} ${node.y}`);
    }
  });

  it('should draw a graticule of chart lines', () => {
    const graticule = routeMotif('a-trip-id').graticule;

    expect(graticule.startsWith('M ')).toBe(true);
    expect(graticule.split('M ').length).toBe(8);
  });

  it('should keep every waypoint on the drawing', () => {
    const { width, height } = plateDimensions('card');

    for (const seed of ['one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight']) {
      for (const node of routeMotif(seed).nodes) {
        expect(node.x).toBeGreaterThanOrEqual(0);
        expect(node.x).toBeLessThanOrEqual(width);
        expect(node.y).toBeGreaterThanOrEqual(0);
        expect(node.y).toBeLessThanOrEqual(height);
      }
    }
  });

  it('should draw a wider band than a card', () => {
    expect(plateDimensions('band').width).toBeGreaterThan(plateDimensions('card').width);
  });

  it('should draw on an empty seed without throwing', () => {
    expect(() => routeMotif('')).not.toThrow();
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
