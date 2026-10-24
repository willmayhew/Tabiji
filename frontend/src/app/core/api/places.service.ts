import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SavedPlace, SavedPlaceDraft } from '../models/place';

/**
 * The places saved to a trip.
 *
 * Every route names the trip as well as the place, so a request can only ever
 * reach a trip's own places. The API answers a place of another trip as
 * though it were not there, so there is nothing extra for this client to
 * check.
 */
@Injectable({ providedIn: 'root' })
export class PlacesService {
  private readonly http = inject(HttpClient);

  /** The places of a trip, in the order they were saved. */
  listForTrip(tripId: string): Observable<SavedPlace[]> {
    return this.http.get<SavedPlace[]>(this.route(tripId));
  }

  create(tripId: string, draft: SavedPlaceDraft): Observable<SavedPlace> {
    return this.http.post<SavedPlace>(this.route(tripId), draft);
  }

  update(tripId: string, placeId: string, draft: SavedPlaceDraft): Observable<SavedPlace> {
    return this.http.put<SavedPlace>(`${this.route(tripId)}/${placeId}`, draft);
  }

  remove(tripId: string, placeId: string): Observable<void> {
    return this.http.delete<void>(`${this.route(tripId)}/${placeId}`);
  }

  private route(tripId: string): string {
    return `${environment.apiBaseUrl}/api/trips/${tripId}/places`;
  }
}
