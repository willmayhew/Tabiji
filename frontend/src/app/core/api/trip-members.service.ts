import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MemberRole, TripMembers } from '../models/member';

/**
 * The people on a trip.
 *
 * The API decides who may read and who may change; this service only carries
 * the request.
 */
@Injectable({ providedIn: 'root' })
export class TripMembersService {
  private readonly http = inject(HttpClient);

  list(tripId: string): Observable<TripMembers> {
    return this.http.get<TripMembers>(`${environment.apiBaseUrl}/api/trips/${tripId}/members`);
  }

  changeRole(tripId: string, userId: string, role: MemberRole): Observable<TripMembers> {
    return this.http.patch<TripMembers>(
      `${environment.apiBaseUrl}/api/trips/${tripId}/members/${userId}`,
      { role },
    );
  }

  remove(tripId: string, userId: string): Observable<void> {
    return this.http.delete<void>(
      `${environment.apiBaseUrl}/api/trips/${tripId}/members/${userId}`,
    );
  }
}
