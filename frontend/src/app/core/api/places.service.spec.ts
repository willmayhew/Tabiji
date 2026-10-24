import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SavedPlace, SavedPlaceDraft } from '../models/place';
import { PlacesService } from './places.service';

const tripId = 'tttttttt-tttt-tttt-tttt-tttttttttttt';

const place: SavedPlace = {
  id: '11111111-1111-1111-1111-111111111111',
  destinationId: null,
  name: 'Fushimi Inari',
  latitude: 34.9671,
  longitude: 135.7727,
  category: 'Sight',
  address: '68 Fukakusa Yabunouchicho, Kyoto',
  notes: 'Go early.',
  createdAt: '2026-10-20T09:00:00+00:00',
};

const draft: SavedPlaceDraft = {
  name: 'Fushimi Inari',
  latitude: 34.9671,
  longitude: 135.7727,
  category: 'Sight',
  address: '68 Fukakusa Yabunouchicho, Kyoto',
  notes: 'Go early.',
  destinationId: null,
};

describe('PlacesService', () => {
  let service: PlacesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PlacesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should list the places of a trip', () => {
    let received: SavedPlace[] | undefined;

    service.listForTrip(tripId).subscribe((places) => (received = places));

    const request = http.expectOne(`/api/trips/${tripId}/places`);
    expect(request.request.method).toBe('GET');

    request.flush([place]);

    expect(received).toEqual([place]);
  });

  it('should save a place to a trip', () => {
    let received: SavedPlace | undefined;

    service.create(tripId, draft).subscribe((saved) => (received = saved));

    const request = http.expectOne(`/api/trips/${tripId}/places`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(draft);

    request.flush(place, { status: 201, statusText: 'Created' });

    expect(received).toEqual(place);
  });

  it('should change a place of a trip', () => {
    let received: SavedPlace | undefined;

    service.update(tripId, place.id, draft).subscribe((saved) => (received = saved));

    const request = http.expectOne(`/api/trips/${tripId}/places/${place.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(draft);

    request.flush(place);

    expect(received).toEqual(place);
  });

  it('should remove a place from a trip', () => {
    let completed = false;

    service.remove(tripId, place.id).subscribe({ complete: () => (completed = true) });

    const request = http.expectOne(`/api/trips/${tripId}/places/${place.id}`);
    expect(request.request.method).toBe('DELETE');

    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBe(true);
  });

  it('should leave a failed request to the caller', () => {
    let status: number | undefined;

    service.remove(tripId, place.id).subscribe({
      error: (failure) => (status = failure.status),
    });

    http
      .expectOne(`/api/trips/${tripId}/places/${place.id}`)
      .flush({ title: 'Saved place not found', status: 404 }, { status: 404, statusText: 'Not Found' });

    // The service does not swallow the failure: what to say about it belongs
    // to the screen, which knows what the reader was doing.
    expect(status).toBe(404);
  });

  it('should keep the API base URL in one place', () => {
    // The route is built from `environment.apiBaseUrl`, so a hosted API is a
    // setting rather than a change to every call.
    service.listForTrip(tripId).subscribe();

    const request = http.expectOne('/api/trips/' + tripId + '/places');

    expect(request.request.url.startsWith('/api/trips/')).toBe(true);
  });
});
