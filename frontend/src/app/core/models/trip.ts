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

/**
 * A stop on a trip's route.
 *
 * The identity is what lets a stop be renamed, moved and rescheduled without
 * losing its place in the trip.
 */
export interface TripDestination {
  id: string;
  name: string;
  sortOrder: number;
  /** The planned first day at the stop, or null when it has not been decided. */
  startsOn: string | null;
  /** The planned last day at the stop, or null when it has not been decided. */
  endsOn: string | null;
}

/** A stop as it is being written. */
export interface TripDestinationDraft {
  name: string;
  startsOn: string | null;
  endsOn: string | null;
}

/**
 * A stop as it is being written for an update. An identifier names a stop
 * that already exists and is kept; no identifier asks for a new one.
 */
export interface TripDestinationUpdateDraft extends TripDestinationDraft {
  id: string | null;
}

export interface Trip {
  id: string;
  name: string;
  destinations: TripDestination[];
  startsOn: string;
  endsOn: string;
  status: TripStatus;
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
}
