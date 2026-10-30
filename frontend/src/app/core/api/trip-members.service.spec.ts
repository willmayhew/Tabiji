import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TripMembersService } from './trip-members.service';

describe('TripMembersService', () => {
  let service: TripMembersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(TripMembersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should read the members of a trip', () => {
    const response = { viewerRole: 'Owner' as const, members: [] };

    service.list('trip-1').subscribe((members) => expect(members).toEqual(response));

    const request = http.expectOne('/api/trips/trip-1/members');

    expect(request.request.method).toBe('GET');
    request.flush(response);
  });

  it('should change a role', () => {
    const response = { viewerRole: 'Owner' as const, members: [] };

    service.changeRole('trip-1', 'user-1', 'Owner').subscribe((members) => {
      expect(members).toEqual(response);
    });

    const request = http.expectOne('/api/trips/trip-1/members/user-1');

    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({ role: 'Owner' });
    request.flush(response);
  });

  it('should remove a member', () => {
    service.remove('trip-1', 'user-1').subscribe();

    const request = http.expectOne('/api/trips/trip-1/members/user-1');

    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });
});
