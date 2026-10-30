import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { InvitationsService } from './invitations.service';

describe('InvitationsService', () => {
  let service: InvitationsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(InvitationsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should list the invitations of a trip', () => {
    service.listForTrip('trip-1').subscribe((invitations) => expect(invitations).toEqual([]));

    const request = http.expectOne('/api/trips/trip-1/invitations');

    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('should create an invitation by address', () => {
    const response = {
      id: 'inv-1',
      tripId: 'trip-1',
      email: 'friend@example.com',
      role: 'Editor',
      status: 'Pending',
      createdAt: '2026-10-30T09:00:00Z',
      expiresAt: '2026-11-06T09:00:00Z',
      invitedByEmail: 'owner@example.com',
      invitationUrl: 'http://localhost:4200/invite#token=abc',
    };

    service.create('trip-1', 'friend@example.com').subscribe((created) => {
      expect(created.invitationUrl).toContain('/invite#token=');
    });

    const request = http.expectOne('/api/trips/trip-1/invitations');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'friend@example.com' });
    request.flush(response);
  });

  it('should revoke an invitation', () => {
    service.revoke('trip-1', 'inv-1').subscribe();

    const request = http.expectOne('/api/trips/trip-1/invitations/inv-1');

    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  it('should list the invitations waiting for the account', () => {
    service.mine().subscribe((invitations) => expect(invitations).toEqual([]));

    const request = http.expectOne('/api/invitations');

    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('should preview, accept and decline a token', () => {
    service.preview('abc').subscribe();
    const preview = http.expectOne('/api/invitations/preview');

    expect(preview.request.method).toBe('POST');
    expect(preview.request.body).toEqual({ token: 'abc' });
    preview.flush({});

    service.accept('abc').subscribe();
    const accept = http.expectOne('/api/invitations/accept');

    expect(accept.request.body).toEqual({ token: 'abc' });
    accept.flush({ tripId: 'trip-1', role: 'Editor' });

    service.decline('abc').subscribe();
    const decline = http.expectOne('/api/invitations/decline');

    expect(decline.request.body).toEqual({ token: 'abc' });
    decline.flush(null);
  });

  it('should accept and decline by invitation id', () => {
    service.acceptById('inv-1').subscribe();
    const accept = http.expectOne('/api/invitations/inv-1/accept');

    expect(accept.request.method).toBe('POST');
    accept.flush({ tripId: 'trip-1', role: 'Editor' });

    service.declineById('inv-1').subscribe();
    const decline = http.expectOne('/api/invitations/inv-1/decline');

    expect(decline.request.method).toBe('POST');
    decline.flush(null);
  });
});
