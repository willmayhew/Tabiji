/**
 * Where the markers of a trip sit, when two of them share a place.
 *
 * A marker is placed by its coordinates, so two stops at the same place would
 * be drawn on exactly the same pixel and only the last of them would be seen.
 * A group that shares a place is therefore spread around a small ring: every
 * one of them stays visible, keeps its own number, and is still the thing the
 * reader clicks.
 *
 * The ring is measured in pixels rather than in degrees. A degree of longitude
 * is a very different distance at the scale of a city and at the scale of a
 * continent, so an offset in degrees would be invisible when zoomed out and
 * absurd when zoomed in.
 */

/**
 * A marker that can share its place with another. Anything carrying a
 * position and an identity will do.
 */
export interface LocatedPoint {
  id: string;
  latitude: number;
  longitude: number;
}

/** Where one marker sits, relative to its own coordinates, in pixels. */
export interface MarkerOffset {
  x: number;
  y: number;
}

/**
 * The room two markers at the same place are given. A marker is twenty-four
 * pixels across, so this leaves a clear gap between them.
 */
const SEPARATION = 30;

/**
 * The widest a group is spread. A place with many things at it fans out to
 * here and no further, so the spread never wanders away from the place.
 */
const MAX_RADIUS = 56;

/**
 * The offset of every marker that shares its place with another, keyed by id.
 *
 * A marker on its own is left out of the map entirely, so it is drawn exactly
 * where it belongs.
 */
export function markerOffsets(points: readonly LocatedPoint[]): Map<string, MarkerOffset> {
  const groups = new Map<string, LocatedPoint[]>();

  for (const point of points) {
    // The same place, as a map and a search give it: the same two numbers.
    const key = `${point.latitude},${point.longitude}`;
    const group = groups.get(key);

    if (group === undefined) {
      groups.set(key, [point]);
    } else {
      group.push(point);
    }
  }

  const offsets = new Map<string, MarkerOffset>();

  for (const group of groups.values()) {
    if (group.length < 2) {
      continue;
    }

    // Neighbours are put far enough apart that they do not touch. Two markers
    // sitting an angle of 2π/n apart on a ring of radius r are 2r·sin(π/n)
    // apart, so the radius that gives them the room they need is
    // (SEPARATION / 2) / sin(π/n).
    const radius = Math.min(MAX_RADIUS, SEPARATION / 2 / Math.sin(Math.PI / group.length));

    group.forEach((point, index) => {
      // From the top and round, in the order the points were given, so the
      // numbers read round the ring the way the route visits them.
      const angle = (2 * Math.PI * index) / group.length - Math.PI / 2;

      offsets.set(point.id, {
        x: Math.round(Math.cos(angle) * radius),
        y: Math.round(Math.sin(angle) * radius),
      });
    });
  }

  return offsets;
}
