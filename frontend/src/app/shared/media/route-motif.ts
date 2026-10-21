/**
 * The route motif.
 *
 * A plate needs a graphic. Rather than a photograph of somewhere, Tabiji
 * draws an abstract route: a line that travels across the plate and passes
 * through a waypoint for each stop on the trip.
 *
 * The motif is derived from a trip's id, never from its destination. That
 * matters twice over. The same trip always draws the same plate, and the
 * plate can never be wrong about where the trip is, because it says nothing
 * about the place at all. It is a graphic device, not a picture of anywhere.
 *
 * This module has no Angular imports and no knowledge of any feature.
 */

/** The grounds a plate can be drawn on. */
export type PlateTone = 'paper' | 'sage' | 'clay' | 'forest';

/** The shapes a plate can take. */
export type PlateAspect = 'card' | 'band';

/** A waypoint on the route. */
export interface RouteNode {
  readonly x: number;
  readonly y: number;
}

/** The drawn route. */
export interface RouteMotif {
  /** The SVG path data for the line. */
  readonly path: string;
  /** The waypoints the line passes through. */
  readonly nodes: readonly RouteNode[];
  /** The faint chart lines that sit behind the route. */
  readonly graticule: string;
}

/** The drawing area of a plate. */
export interface PlateDimensions {
  readonly width: number;
  readonly height: number;
}

/**
 * The drawing area for each shape.
 *
 * The card is 4:3 and the band is 21:4, and the view box matches the shape
 * exactly, so a drawing is never distorted by the aspect ratio.
 */
const ASPECTS: Record<PlateAspect, PlateDimensions> = {
  card: { width: 120, height: 90 },
  band: { width: 210, height: 40 },
};

/** The grounds, in a fixed order so the mapping from a hash is stable. */
const TONES: readonly PlateTone[] = ['paper', 'sage', 'clay', 'forest'];

/** FNV-1a, 32 bit. A small hash that spreads short strings evenly. */
export function hashSeed(seed: string): number {
  let hash = 0x811c9dc5;

  for (let index = 0; index < seed.length; index += 1) {
    hash ^= seed.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }

  return hash >>> 0;
}

/** A small deterministic generator, seeded from the hash. */
function createRandom(seed: number): () => number {
  let state = seed >>> 0;

  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let value = state;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);

    return ((value ^ (value >>> 14)) >>> 0) / 4294967296;
  };
}

/** Rounds to two decimals so the path data stays readable. */
function round(value: number): number {
  return Math.round(value * 100) / 100;
}

/** Keeps a coordinate on the drawing. */
function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

/**
 * Keeps a value inside a range, bouncing off the ends rather than stopping
 * at them, so a wandering route never piles up along one edge.
 */
function reflect(value: number, min: number, max: number): number {
  if (value < min) {
    return min + (min - value);
  }

  if (value > max) {
    return max - (value - max);
  }

  return value;
}

/**
 * The line, drawn as a smooth curve through the waypoints.
 *
 * A Catmull-Rom spline is converted into cubic segments, so the route bends
 * the way a plotted line bends instead of kinking at every waypoint.
 */
function toPath(nodes: readonly RouteNode[]): string {
  if (nodes.length === 0) {
    return '';
  }

  if (nodes.length === 1) {
    return `M ${nodes[0].x} ${nodes[0].y}`;
  }

  let path = `M ${nodes[0].x} ${nodes[0].y}`;

  for (let index = 0; index < nodes.length - 1; index += 1) {
    const previous = nodes[Math.max(0, index - 1)];
    const current = nodes[index];
    const next = nodes[index + 1];
    const after = nodes[Math.min(nodes.length - 1, index + 2)];

    const control1 = {
      x: current.x + (next.x - previous.x) / 6,
      y: current.y + (next.y - previous.y) / 6,
    };
    const control2 = {
      x: next.x - (after.x - current.x) / 6,
      y: next.y - (after.y - current.y) / 6,
    };

    path +=
      ` C ${round(control1.x)} ${round(control1.y)}` +
      ` ${round(control2.x)} ${round(control2.y)}` +
      ` ${next.x} ${next.y}`;
  }

  return path;
}

/** The chart lines behind the route. Four verticals and three horizontals. */
function toGraticule(
  width: number,
  height: number,
  random: () => number,
): string {
  const lines: string[] = [];

  for (let index = 1; index <= 4; index += 1) {
    const x = round(clamp(width * (index / 5) + (random() - 0.5) * width * 0.06, 2, width - 2));
    lines.push(`M ${x} 0 L ${x} ${height}`);
  }

  for (let index = 1; index <= 3; index += 1) {
    const y = round(clamp(height * (index / 4) + (random() - 0.5) * height * 0.08, 2, height - 2));
    lines.push(`M 0 ${y} L ${width} ${y}`);
  }

  return lines.join(' ');
}

/**
 * The route for a seed.
 *
 * The line travels from left to right, so it reads as a journey rather than
 * as a scribble. It carries one waypoint for each stop on the trip, and
 * never fewer than two, so that a trip with a single stop still has a line
 * to draw.
 */
export function routeMotif(seed: string, stops: number, aspect: PlateAspect = 'card'): RouteMotif {
  const { width, height } = ASPECTS[aspect];
  const hash = hashSeed(seed);
  const random = createRandom(hash);

  const nodeCount = Math.max(2, stops);

  // The waypoints are spread evenly across the plate. The jitter is kept
  // below a quarter of the gap between them, so the route never doubles
  // back on itself however many stops there are.
  const span = 0.84;
  const gap = span / (nodeCount - 1);
  const jitter = Math.min(0.06, gap * 0.25);

  // The route wanders: each waypoint is a bounded step from the last one,
  // so a long route reads as a journey rather than as a scribble.
  const margin = height * 0.14;
  const limit = height - margin;
  const maxStep = height * 0.28;

  const nodes: RouteNode[] = [];
  let y = margin + random() * (limit - margin);

  for (let index = 0; index < nodeCount; index += 1) {
    const x =
      width * (0.08 + span * (index / (nodeCount - 1))) +
      (random() - 0.5) * width * jitter;

    if (index > 0) {
      y = reflect(y + (random() - 0.5) * maxStep, margin, limit);
    }

    nodes.push({
      x: round(clamp(x, 4, width - 4)),
      y: round(clamp(y, margin, limit)),
    });
  }

  return { path: toPath(nodes), nodes, graticule: toGraticule(width, height, random) };
}

/**
 * The ground a plate is drawn on.
 *
 * Derived from a second hash of the seed, so the ground is independent of
 * the route: a trip always gets the same pair.
 */
export function plateTone(seed: string): PlateTone {
  return TONES[hashSeed(`${seed}|tone`) % TONES.length];
}

/** The drawing area for a shape. */
export function plateDimensions(aspect: PlateAspect): PlateDimensions {
  return ASPECTS[aspect];
}
