export type TripStatus = 'Planning' | 'Confirmed' | 'Completed' | 'Cancelled';

/**
 * Mirrors the API contract. The server enforces the same limits in the domain
 * (Tabiji.Domain.Trips.Trip.NameMaxLength and Tabiji.Domain.Trips.Trip.DestinationMaxLength).
 */
export const TRIP_NAME_MAX_LENGTH = 100;
export const TRIP_DESTINATION_MAX_LENGTH = 200;

export interface Trip {
  id: string;
  name: string;
  destination: string;
  startsOn: string;
  endsOn: string;
  status: TripStatus;
}

export interface CreateTrip {
  name: string;
  destination: string;
  startsOn: string;
  endsOn: string;
}
