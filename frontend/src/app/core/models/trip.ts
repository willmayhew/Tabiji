export type TripStatus = 'Planning' | 'Confirmed' | 'Completed' | 'Cancelled';

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
