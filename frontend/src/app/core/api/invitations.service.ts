import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AcceptedInvitation,
  CreatedInvitation,
  InvitationPreview,
  RecipientInvitation,
  TripInvitation,
} from '../models/member';

/**
 * Invitations, from both sides: the owner's list for a trip and the
 * recipient's pending list and decisions.
 *
 * The token travels in the request body, never in the URL path. The one place
 * a token appears in a URL is the development link, where it is in the
 * fragment.
 */
@Injectable({ providedIn: 'root' })
export class InvitationsService {
  private readonly http = inject(HttpClient);

  listForTrip(tripId: string): Observable<TripInvitation[]> {
    return this.http.get<TripInvitation[]>(
      `${environment.apiBaseUrl}/api/trips/${tripId}/invitations`,
    );
  }

  create(tripId: string, email: string): Observable<CreatedInvitation> {
    return this.http.post<CreatedInvitation>(
      `${environment.apiBaseUrl}/api/trips/${tripId}/invitations`,
      { email },
    );
  }

  revoke(tripId: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(
      `${environment.apiBaseUrl}/api/trips/${tripId}/invitations/${invitationId}`,
    );
  }

  mine(): Observable<RecipientInvitation[]> {
    return this.http.get<RecipientInvitation[]>(`${environment.apiBaseUrl}/api/invitations`);
  }

  preview(token: string): Observable<InvitationPreview> {
    return this.http.post<InvitationPreview>(
      `${environment.apiBaseUrl}/api/invitations/preview`,
      { token },
    );
  }

  accept(token: string): Observable<AcceptedInvitation> {
    return this.http.post<AcceptedInvitation>(
      `${environment.apiBaseUrl}/api/invitations/accept`,
      { token },
    );
  }

  decline(token: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/api/invitations/decline`,
      { token },
    );
  }

  acceptById(invitationId: string): Observable<AcceptedInvitation> {
    return this.http.post<AcceptedInvitation>(
      `${environment.apiBaseUrl}/api/invitations/${invitationId}/accept`,
      {},
    );
  }

  declineById(invitationId: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/api/invitations/${invitationId}/decline`,
      {},
    );
  }
}
