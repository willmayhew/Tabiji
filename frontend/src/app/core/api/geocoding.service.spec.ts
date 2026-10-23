import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { PlaceSuggestion } from '../models/place';
import { GeocodingService } from './geocoding.service';

describe('GeocodingService', () => {
  let service: GeocodingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(GeocodingService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should ask the provider for places matching a query', () => {
    let places: PlaceSuggestion[] = [];
    service.search('kyoto').subscribe((result) => (places = result));

    const request = http.expectOne((probe) => probe.url === `${environment.map.geocodingBaseUrl}/api`);

    expect(request.request.params.get('q')).toBe('kyoto');
    expect(request.request.params.get('limit')).toBe('6');

    request.flush({
      features: [
        {
          geometry: { coordinates: [135.7681, 35.0116] },
          properties: { name: 'Kyoto', city: 'Kyoto', country: 'Japan' },
        },
      ],
    });

    expect(places).toEqual([
      {
        name: 'Kyoto',
        destinationName: 'Kyoto',
        label: 'Kyoto, Japan',
        latitude: 35.0116,
        longitude: 135.7681,
      },
    ]);
  });

  it('should name a place with the locality that tells it apart', () => {
    let places: PlaceSuggestion[] = [];
    service.search('asda').subscribe((result) => (places = result));

    http.expectOne((probe) => probe.url === `${environment.map.geocodingBaseUrl}/api`).flush({
      features: [
        {
          geometry: { coordinates: [-1.9868, 50.7182] },
          properties: {
            name: 'Asda',
            district: 'Poole',
            city: 'Poole',
            state: 'England',
            country: 'United Kingdom',
          },
        },
      ],
    });

    expect(places).toEqual([
      {
        name: 'Asda',
        destinationName: 'Asda, Poole',
        label: 'Asda, Poole, England, United Kingdom',
        latitude: 50.7182,
        longitude: -1.9868,
      },
    ]);
  });

  it('should trim the query before asking', () => {
    service.search('  kyoto  ').subscribe();

    const request = http.expectOne((probe) => probe.url === `${environment.map.geocodingBaseUrl}/api`);

    expect(request.request.params.get('q')).toBe('kyoto');
    request.flush({ features: [] });
  });

  it('should not ask the provider for an empty query', () => {
    let places: PlaceSuggestion[] | null = null;
    service.search('   ').subscribe((result) => (places = result));

    http.expectNone((probe) => probe.url === `${environment.map.geocodingBaseUrl}/api`);
    expect(places).toEqual([]);
  });

  it('should drop features that carry no coordinates or no name', () => {
    let places: PlaceSuggestion[] = [];
    service.search('kyoto').subscribe((result) => (places = result));

    http.expectOne((probe) => probe.url === `${environment.map.geocodingBaseUrl}/api`).flush({
      features: [
        { geometry: { coordinates: [135.7681, 35.0116] }, properties: {} },
        { properties: { name: 'Nowhere' } },
        {
          geometry: { coordinates: [135.7681, 35.0116] },
          properties: { name: 'Kyoto' },
        },
      ],
    });

    expect(places).toEqual([
      {
        name: 'Kyoto',
        destinationName: 'Kyoto',
        label: 'Kyoto',
        latitude: 35.0116,
        longitude: 135.7681,
      },
    ]);
  });

  it('should list a place only once', () => {
    let places: PlaceSuggestion[] = [];
    service.search('kyoto').subscribe((result) => (places = result));

    http.expectOne((probe) => probe.url === `${environment.map.geocodingBaseUrl}/api`).flush({
      features: [
        {
          geometry: { coordinates: [135.7681, 35.0116] },
          properties: { name: 'Kyoto', city: 'Kyoto', country: 'Japan' },
        },
        {
          geometry: { coordinates: [135.7682, 35.0117] },
          properties: { name: 'Kyoto', city: 'Kyoto', country: 'Japan' },
        },
        {
          geometry: { coordinates: [135.7681, 35.0116] },
          properties: { name: 'Osaka', country: 'Japan' },
        },
      ],
    });

    expect(places).toEqual([
      {
        name: 'Kyoto',
        destinationName: 'Kyoto',
        label: 'Kyoto, Japan',
        latitude: 35.0116,
        longitude: 135.7681,
      },
      {
        name: 'Osaka',
        destinationName: 'Osaka',
        label: 'Osaka, Japan',
        latitude: 35.0116,
        longitude: 135.7681,
      },
    ]);
  });

  it('should name a point from the reverse endpoint', () => {
    let place: PlaceSuggestion | null | undefined;
    service.reverse(35.0116, 135.7681).subscribe((result) => (place = result));

    const request = http.expectOne(
      (probe) => probe.url === `${environment.map.geocodingBaseUrl}/reverse`,
    );

    expect(request.request.params.get('lat')).toBe('35.0116');
    expect(request.request.params.get('lon')).toBe('135.7681');

    request.flush({
      features: [
        {
          geometry: { coordinates: [135.7681, 35.0116] },
          properties: { name: 'Kyoto', country: 'Japan' },
        },
      ],
    });

    expect(place).toEqual({
      name: 'Kyoto',
      destinationName: 'Kyoto',
      label: 'Kyoto, Japan',
      latitude: 35.0116,
      longitude: 135.7681,
    });
  });

  it('should return nothing when a point names no place', () => {
    let place: PlaceSuggestion | null | undefined;
    service.reverse(0, 0).subscribe((result) => (place = result));

    http
      .expectOne((probe) => probe.url === `${environment.map.geocodingBaseUrl}/reverse`)
      .flush({ features: [] });

    expect(place).toBeNull();
  });
});
