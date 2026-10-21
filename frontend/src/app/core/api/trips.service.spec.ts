import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TripsService } from './trips.service';

describe('TripsService', () => {
  let service: TripsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(TripsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should list every trip', () => {
    let names: string[] = [];

    service.list().subscribe((trips) => (names = trips.map((trip) => trip.name)));

    const request = http.expectOne('/api/trips');
    expect(request.request.method).toBe('GET');
    request.flush([
      {
        id: '5f2b1c4e-0000-0000-0000-000000000000',
        name: 'Rome & Amalfi',
        destinations: [
          { name: 'Rome', sortOrder: 0 },
          { name: 'Amalfi', sortOrder: 1 },
        ],
        startsOn: '2027-04-01',
        endsOn: '2027-04-14',
        status: 'Planning',
      },
    ]);

    expect(names).toEqual(['Rome & Amalfi']);
  });

  it('should post a new trip', () => {
    let name: string | undefined;

    service
      .create({
        name: 'Rome & Amalfi',
        destinations: ['Rome', 'Amalfi'],
        startsOn: '2027-04-01',
        endsOn: '2027-04-14',
      })
      .subscribe((trip) => (name = trip.name));

    const request = http.expectOne('/api/trips');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Rome & Amalfi',
      destinations: ['Rome', 'Amalfi'],
      startsOn: '2027-04-01',
      endsOn: '2027-04-14',
    });

    request.flush({
      id: '5f2b1c4e-0000-0000-0000-000000000000',
      name: 'Rome & Amalfi',
      destinations: [
        { name: 'Rome', sortOrder: 0 },
        { name: 'Amalfi', sortOrder: 1 },
      ],
      startsOn: '2027-04-01',
      endsOn: '2027-04-14',
      status: 'Planning',
    });

    expect(name).toBe('Rome & Amalfi');
  });

  it('should request a trip by id', () => {
    let status: string | undefined;

    service.getById('5f2b1c4e-0000-0000-0000-000000000000').subscribe((trip) => (status = trip.status));

    const request = http.expectOne('/api/trips/5f2b1c4e-0000-0000-0000-000000000000');
    expect(request.request.method).toBe('GET');
    request.flush({
      id: '5f2b1c4e-0000-0000-0000-000000000000',
      name: 'Rome & Amalfi',
      destinations: [
        { name: 'Rome', sortOrder: 0 },
        { name: 'Amalfi', sortOrder: 1 },
      ],
      startsOn: '2027-04-01',
      endsOn: '2027-04-14',
      status: 'Planning',
    });

    expect(status).toBe('Planning');
  });
});
