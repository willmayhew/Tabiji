import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateTrip, Trip, UpdateTrip } from '../models/trip';

@Injectable({ providedIn: 'root' })
export class TripsService {
  private readonly http = inject(HttpClient);

  list(): Observable<Trip[]> {
    return this.http.get<Trip[]>(`${environment.apiBaseUrl}/api/trips`);
  }

  create(request: CreateTrip): Observable<Trip> {
    return this.http.post<Trip>(`${environment.apiBaseUrl}/api/trips`, request);
  }

  getById(id: string): Observable<Trip> {
    return this.http.get<Trip>(`${environment.apiBaseUrl}/api/trips/${id}`);
  }

  /**
   * Replaces a trip and its route in one go. The request holds the whole
   * trip: the name, the dates and the stops, in the order they are visited.
   */
  update(id: string, request: UpdateTrip): Observable<Trip> {
    return this.http.put<Trip>(`${environment.apiBaseUrl}/api/trips/${id}`, request);
  }
}
