import type { PlaceCategory } from './place';

export type TripStatus = 'Planning' | 'Confirmed' | 'Completed' | 'Cancelled';

/**
 * Mirrors the API contract. The server enforces the same limits in the domain
 * (Tabiji.Domain.Trips.Trip.NameMaxLength and
 * Tabiji.Domain.Trips.TripDestination.NameMaxLength) and the same maximum
 * number of stops (Tabiji.Domain.Trips.Trip.MaxDestinationCount).
 */
export const TRIP_NAME_MAX_LENGTH = 100;
export const TRIP_DESTINATION_MAX_LENGTH = 200;
export const TRIP_MAX_DESTINATIONS = 20;
export const TRIP_MAX_STOPS_PER_DESTINATION = 20;
export const PLACE_ADDRESS_MAX_LENGTH = 300;
export const PLACE_NOTES_MAX_LENGTH = 1000;

/**
 * A place on a trip: a destination on the main route, or a stop within one.
 *
 * The identity is what lets a place be renamed, moved and rescheduled without
 * losing its place in the trip. A stop's `sortOrder` is its position within
 * its destination, not a place on the main route.
 */
export interface TripStop {
  id: string;
  name: string;
  sortOrder: number;
  /** The planned first day at the place, or null when it has not been decided. */
  startsOn: string | null;
  /** The planned last day at the place, or null when it has not been decided. */
  endsOn: string | null;
  /**
   * The latitude of a place chosen from a map, or null when the place was
   * typed by hand. The name is always the primary value; the coordinates are
   * only kept when they belong to that name.
   */
  latitude: number | null;
  /** The longitude of a place chosen from a map, or null when it was typed by hand. */
  longitude: number | null;
  /** What kind of place it is, or null when the reader has not said. */
  category?: PlaceCategory | null;
  /** A short description of where it is, or null. */
  address?: string | null;
  /** The reader's own note, or null. */
  notes?: string | null;
  /** The day of the trip it is planned for, or null. */
  plannedOn?: string | null;
}

/**
 * A destination on the main route, with the stops within it.
 *
 * `stops` is optional so that a response written before the hierarchy — or a
 * hand-written fixture — is still readable. The API always sends it.
 */
export interface TripDestination extends TripStop {
  stops?: TripStop[];
}

/** The stops of a destination, whether or not the response carried them. */
export function stopsOf(destination: TripDestination): TripStop[] {
  return destination.stops ?? [];
}

/** Every stop on a trip, in destination order and then stop order. */
export function allStops(destinations: readonly TripDestination[]): TripStop[] {
  return destinations.flatMap((destination) => stopsOf(destination));
}

/**
 * Finds the place a saved place is filed under, whether that is a destination
 * on the main route or a stop within one.
 */
export function findFiling(
  destinations: readonly TripDestination[],
  id: string,
): { destination: TripDestination; stop: TripStop | null } | null {
  for (const destination of destinations) {
    if (destination.id === id) {
      return { destination, stop: null };
    }

    const stop = stopsOf(destination).find((candidate) => candidate.id === id);

    if (stop !== undefined) {
      return { destination, stop };
    }
  }

  return null;
}

/** A stop as it is being written. */
export interface TripStopDraft {
  name: string;
  startsOn: string | null;
  endsOn: string | null;
  latitude: number | null;
  longitude: number | null;
  category?: PlaceCategory | null;
  address?: string | null;
  notes?: string | null;
  plannedOn?: string | null;
}

/**
 * A stop as it is being written for an update. An identifier names a stop
 * that already exists and is kept; no identifier asks for a new one.
 */
export interface TripStopUpdateDraft extends TripStopDraft {
  id: string | null;
}

/** A destination as it is being written, with the stops within it. */
export interface TripDestinationDraft {
  name: string;
  startsOn: string | null;
  endsOn: string | null;
  latitude: number | null;
  longitude: number | null;
  stops: TripStopDraft[];
  category?: PlaceCategory | null;
  address?: string | null;
  notes?: string | null;
  plannedOn?: string | null;
}

/**
 * A destination as it is being written for an update. An identifier names a
 * destination that already exists and is kept; no identifier asks for a new
 * one. A destination left out is removed with its stops.
 */
export interface TripDestinationUpdateDraft extends Omit<TripDestinationDraft, 'stops'> {
  id: string | null;
  stops: TripStopUpdateDraft[];
}

export interface Trip {
  id: string;
  name: string;
  destinations: TripDestination[];
  startsOn: string;
  endsOn: string;
  status: TripStatus;
  /**
   * Legacy saved places that have not been filed under a destination. They
   * are kept readable, but new locations are always added as stops.
   */
  unfiledPlaces?: TripStop[];
}

/** The trip's legacy unfiled places, whether or not the response carried them. */
export function unfiledPlacesOf(trip: Trip): TripStop[] {
  return trip.unfiledPlaces ?? [];
}

export interface CreateTrip {
  name: string;
  startsOn: string;
  endsOn: string;
  destinations: TripDestinationDraft[];
}

export interface UpdateTrip {
  name: string;
  startsOn: string;
  endsOn: string;
  destinations: TripDestinationUpdateDraft[];
  unfiledPlaces?: TripStopUpdateDraft[];
}
