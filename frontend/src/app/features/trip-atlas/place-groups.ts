import { SavedPlace } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';

/**
 * The places of one stop, or the places filed under no stop.
 */
export interface PlaceGroup {
  /** The stop the group belongs to, or null for the places filed under none. */
  destination: TripDestination | null;
  places: SavedPlace[];
}

/**
 * Groups a trip's saved places by the stop they are filed under.
 *
 * The groups follow the stops in the order they are given — the order the
 * route is travelled — so the plan reads the way the journey runs. The places
 * filed under no stop come last, in a group of their own.
 *
 * A place whose stop is not in the trip is grouped as unfiled. A stop can be
 * removed from the trip at any time, and a place must never fall out of the
 * list because the stop it was filed under has gone: it is still saved to the
 * trip, and it still needs filing.
 *
 * A stop with nothing filed under it produces no group at all. The plan shows
 * what is in it, not what is missing from it.
 */
export function groupPlacesByStop(
  destinations: readonly TripDestination[],
  places: readonly SavedPlace[],
): PlaceGroup[] {
  const known = new Set(destinations.map((destination) => destination.id));
  const filed = new Map<string, SavedPlace[]>();
  const unfiled: SavedPlace[] = [];

  for (const place of places) {
    const stopId = place.destinationId;

    if (stopId === null || !known.has(stopId)) {
      unfiled.push(place);
      continue;
    }

    const group = filed.get(stopId);

    if (group === undefined) {
      filed.set(stopId, [place]);
    } else {
      group.push(place);
    }
  }

  const groups: PlaceGroup[] = [];

  for (const destination of destinations) {
    const placesAtStop = filed.get(destination.id);

    if (placesAtStop !== undefined) {
      groups.push({ destination, places: placesAtStop });
    }
  }

  if (unfiled.length > 0) {
    groups.push({ destination: null, places: unfiled });
  }

  return groups;
}
