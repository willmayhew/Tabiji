import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TripsService } from './trips.service';

const trip = {
  id: '5f2b1c4e-0000-0000-0000-000000000000',
  name: 'Rome & Amalfi',
  destinations: [
    {
      id: '0a1b2c3d-0000-0000-0000-000000000000',
      name: 'Rome',
      sortOrder: 0,
      startsOn: '2027-04-01',
      endsOn: '2027-04-05',
      latitude: null,
      longitude: null,
    },
    {
      id: '0a1b2c3d-0000-0000-0000-000000000001',
      name: 'Amalfi',
      sortOrder: 1,
      startsOn: null,
      endsOn: null,
      latitude: null,
      longitude: null,
    },
  ],
  startsOn: '2027-04-01',
  endsOn: '2027-04-14',
  status: 'Planning',
};

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
    request.flush([trip]);

    expect(names).toEqual(['Rome & Amalfi']);
  });

  it('should post a new trip', () => {
    let name: string | undefined;

    service
      .create({
        name: 'Rome & Amalfi',
        destinations: [
          { name: 'Rome', startsOn: '2027-04-01', endsOn: '2027-04-05', latitude: null, longitude: null, stops: [] },
          { name: 'Amalfi', startsOn: null, endsOn: null, latitude: null, longitude: null, stops: [] },
        ],
        startsOn: '2027-04-01',
        endsOn: '2027-04-14',
      })
      .subscribe((created) => (name = created.name));

    const request = http.expectOne('/api/trips');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Rome & Amalfi',
      destinations: [
        { name: 'Rome', startsOn: '2027-04-01', endsOn: '2027-04-05', latitude: null, longitude: null, stops: [] },
        { name: 'Amalfi', startsOn: null, endsOn: null, latitude: null, longitude: null, stops: [] },
      ],
      startsOn: '2027-04-01',
      endsOn: '2027-04-14',
    });

    request.flush(trip);

    expect(name).toBe('Rome & Amalfi');
  });

  it('should request a trip by id', () => {
    let status: string | undefined;

    service.getById('5f2b1c4e-0000-0000-0000-000000000000').subscribe((found) => (status = found.status));

    const request = http.expectOne('/api/trips/5f2b1c4e-0000-0000-0000-000000000000');
    expect(request.request.method).toBe('GET');
    request.flush(trip);

    expect(status).toBe('Planning');
  });

  it('should put the whole trip back', () => {
    let name: string | undefined;

    service
      .update('5f2b1c4e-0000-0000-0000-000000000000', {
        name: 'Italia',
        destinations: [
          { id: '0a1b2c3d-0000-0000-0000-000000000000', name: 'Roma', startsOn: null, endsOn: null, latitude: null, longitude: null, stops: [] },
          { id: null, name: 'Napoli', startsOn: '2027-04-05', endsOn: '2027-04-08', latitude: null, longitude: null, stops: [] },
        ],
        startsOn: '2027-04-01',
        endsOn: '2027-04-14',
      })
      .subscribe((updated) => (name = updated.name));

    const request = http.expectOne('/api/trips/5f2b1c4e-0000-0000-0000-000000000000');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Italia',
      destinations: [
        { id: '0a1b2c3d-0000-0000-0000-000000000000', name: 'Roma', startsOn: null, endsOn: null, latitude: null, longitude: null, stops: [] },
        { id: null, name: 'Napoli', startsOn: '2027-04-05', endsOn: '2027-04-08', latitude: null, longitude: null, stops: [] },
      ],
      startsOn: '2027-04-01',
      endsOn: '2027-04-14',
    });

    request.flush({ ...trip, name: 'Italia' });

    expect(name).toBe('Italia');
  });
});
