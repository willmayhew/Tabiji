import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateTrip, Trip } from '../models/trip';

@Injectable({ providedIn: 'root' })
export class TripsService {
  private readonly http = inject(HttpClient);

  create(request: CreateTrip): Observable<Trip> {
    return this.http.post<Trip>(`${environment.apiBaseUrl}/api/trips`, request);
  }

  getById(id: string): Observable<Trip> {
    return this.http.get<Trip>(`${environment.apiBaseUrl}/api/trips/${id}`);
  }
}
