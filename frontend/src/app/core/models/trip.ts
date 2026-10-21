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

/** A stop on a trip's route. */
export interface TripDestination {
  name: string;
  sortOrder: number;
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
  destinations: string[];
  startsOn: string;
  endsOn: string;
}
