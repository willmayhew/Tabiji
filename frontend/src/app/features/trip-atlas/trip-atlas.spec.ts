import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { GeocodingService } from '../../core/api/geocoding.service';
import { PlaceSuggestion } from '../../core/models/place';
import { Trip, UpdateTrip } from '../../core/models/trip';
import { AtlasHandle, AtlasPoint, MapEngine, MapHandle } from '../../shared/map/map-engine';
import { TripAtlas } from './trip-atlas';

const kyoto: PlaceSuggestion = {
  name: 'Kyoto',
  destinationName: 'Kyoto, Japan',
  label: 'Kyoto, Japan',
  latitude: 35.0116,
  longitude: 135.7681,
};

const trip: Trip = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Italy & Iceland',
  destinations: [
    {
      id: 'a1',
      name: 'Rome',
      sortOrder: 0,
      startsOn: '2027-04-01',
      endsOn: '2027-04-04',
      latitude: 41.9028,
      longitude: 12.4964,
    },
    {
      id: 'b2',
      name: 'Amalfi',
      sortOrder: 1,
      startsOn: null,
      endsOn: null,
      latitude: null,
      longitude: null,
    },
    {
      id: 'c3',
      name: 'Naples',
      sortOrder: 2,
      startsOn: null,
      endsOn: null,
      latitude: null,
      longitude: null,
    },
    {
      id: 'd4',
      name: 'Reykjavík',
      sortOrder: 3,
      startsOn: '2027-04-06',
      endsOn: '2027-04-09',
      latitude: 64.1466,
      longitude: -21.9426,
    },
    {
      id: 'e5',
      name: 'Vík',
      sortOrder: 4,
      startsOn: null,
      endsOn: null,
      latitude: null,
      longitude: null,
    },
  ],
  startsOn: '2027-04-01',
  endsOn: '2027-04-10',
  status: 'Planning',
};

class FakeGeocodingService {
  search = (): Observable<PlaceSuggestion[]> => of([kyoto]);
  reverse = (): Observable<PlaceSuggestion | null> => of(null);
}

class FakeMapEngine {
  unavailable = false;
  fitted = 0;
  readonly drawn: AtlasPoint[][] = [];
  selectHandler: ((id: string) => void) | null = null;

  handle: AtlasHandle = {
    setDestinations: (points) => this.drawn.push([...points]),
    setRoute: () => undefined,
    setSelection: () => undefined,
    fitToTrip: () => {
      this.fitted += 1;
    },
    resize: () => undefined,
    onSelect: (handler) => {
      this.selectHandler = handler;
    },
    onError: () => undefined,
    destroy: () => undefined,
  };

  // The picker asks for a single-marker map; the atlas asks for the trip.
  create = (): Promise<MapHandle | null> => Promise.resolve(null);

  createAtlas = (): Promise<AtlasHandle | null> =>
    Promise.resolve(this.unavailable ? null : this.handle);
}

describe('TripAtlas', () => {
  let fixture: ComponentFixture<TripAtlas>;
  let http: HttpTestingController;
  let paramMap: BehaviorSubject<ParamMap>;
  let engine: FakeMapEngine;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    paramMap = new BehaviorSubject<ParamMap>(convertToParamMap({ id: '' }));
    engine = new FakeMapEngine();

    await TestBed.configureTestingModule({
      imports: [TripAtlas],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap } },
        { provide: MapEngine, useValue: engine },
        { provide: GeocodingService, useValue: new FakeGeocodingService() },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function button(text: string): HTMLButtonElement {
    const found = [...element().querySelectorAll('button')].find((candidate) =>
      candidate.textContent?.includes(text),
    );

    return found as HTMLButtonElement;
  }

  function rows(): HTMLButtonElement[] {
    return [...element().querySelectorAll('.atlas-list__item')] as HTMLButtonElement[];
  }

  async function settle(milliseconds = 0): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, milliseconds));
    fixture.detectChanges();
  }

  function createComponent(id: string): void {
    paramMap.next(convertToParamMap({ id }));
    fixture = TestBed.createComponent(TripAtlas);
    fixture.detectChanges();
  }

  /** Loads the trip and leaves the page showing it. */
  async function showTrip(loaded: Trip = trip): Promise<void> {
    createComponent(trip.id);

    http.expectOne(`/api/trips/${trip.id}`).flush(loaded);
    fixture.detectChanges();
    await fixture.whenStable();
    await settle();
  }

  function stop(partial: Partial<Trip>): Trip {
    return { ...trip, ...partial };
  }

  it('should show a loading state while the trip is fetched', () => {
    createComponent(trip.id);

    expect(element().textContent).toContain('Loading the atlas');

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();
  });

  it('should show the trip name as the only heading', async () => {
    await showTrip();

    const headings = element().querySelectorAll('h1');

    expect(headings).toHaveLength(1);
    expect(headings[0].textContent).toContain('Italy & Iceland');
  });

  it('should list every stop, including the ones with no location', async () => {
    await showTrip();

    expect(rows()).toHaveLength(5);
    expect(element().textContent).toContain('Vík');
  });

  it('should draw only the located stops on the map', async () => {
    await showTrip();

    expect(engine.drawn.at(-1)?.map((point) => point.id)).toEqual(['a1', 'd4']);
  });

  it('should show an empty map state when nothing is located', async () => {
    await showTrip(
      stop({
        destinations: trip.destinations.map((destination) => ({
          ...destination,
          latitude: null,
          longitude: null,
        })),
      }),
    );

    expect(element().textContent).toContain('Nothing on the map yet');
    expect(element().querySelector('app-trip-atlas-map')).toBeNull();
  });

  it('should own up to a route that is only partly on the map', async () => {
    await showTrip();

    expect(element().textContent).toContain('2 of 5 stops are on the map');
  });

  it('should select a stop from the list and show its details', async () => {
    await showTrip();

    rows()[1].click();
    fixture.detectChanges();

    expect(rows()[1].getAttribute('aria-pressed')).toBe('true');
    expect(element().querySelector('.atlas-panel__name')?.textContent).toContain('Amalfi');
    expect(element().textContent).toContain('Stop 2 of 5');
    expect(element().textContent).toContain('Not on the map');
  });

  it('should select a stop from the map', async () => {
    await showTrip();

    engine.selectHandler?.('d4');
    fixture.detectChanges();

    const pressed = rows().filter((row) => row.getAttribute('aria-pressed') === 'true');

    expect(pressed).toHaveLength(1);
    expect(pressed[0].textContent).toContain('Reykjavík');
  });

  it('should say where the selected stop is', async () => {
    await showTrip();

    rows()[0].click();
    fixture.detectChanges();

    expect(element().textContent).toContain('On the map');
    expect(element().textContent).toContain('1 Apr – 4 Apr 2027');
  });

  it('should frame the trip when it is asked to', async () => {
    await showTrip();

    engine.fitted = 0;
    button('Frame the trip').click();

    expect(engine.fitted).toBe(1);
  });

  it('should take turns between the map and the list on a narrow screen', async () => {
    await showTrip();

    expect(element().querySelector('.atlas__body')?.classList.contains('atlas__body--map')).toBe(
      true,
    );

    button('List').click();
    fixture.detectChanges();

    expect(element().querySelector('.atlas__body')?.classList.contains('atlas__body--list')).toBe(
      true,
    );
    expect(button('List').getAttribute('aria-pressed')).toBe('true');
  });

  it('should change nothing when a new location is cancelled', async () => {
    await showTrip();

    rows()[1].click();
    fixture.detectChanges();

    button('Change location').click();
    fixture.detectChanges();
    await settle();

    button('Cancel').click();
    fixture.detectChanges();
    await settle();

    http.expectNone(`/api/trips/${trip.id}`);
    expect(element().textContent).toContain('Amalfi');
  });

  it('should give a confirmed location to the stop it was chosen for, and no other', async () => {
    await showTrip();

    rows()[1].click();
    fixture.detectChanges();

    button('Change location').click();
    fixture.detectChanges();
    await settle();

    const input = element().querySelector('.picker__input') as HTMLInputElement;
    input.value = 'Kyoto';
    input.dispatchEvent(new Event('input'));
    await settle(350);

    (element().querySelector('.picker__result') as HTMLButtonElement).click();
    fixture.detectChanges();
    button('Use this location').click();
    fixture.detectChanges();

    const request = http.expectOne(`/api/trips/${trip.id}`);

    expect(request.request.method).toBe('PUT');

    const body = request.request.body as UpdateTrip;

    expect(body.destinations.find((destination) => destination.id === 'b2')).toEqual({
      id: 'b2',
      name: 'Kyoto, Japan',
      startsOn: null,
      endsOn: null,
      latitude: 35.0116,
      longitude: 135.7681,
    });

    // Everything else is left exactly as it was.
    expect(body.destinations.find((destination) => destination.id === 'a1')).toEqual({
      id: 'a1',
      name: 'Rome',
      startsOn: '2027-04-01',
      endsOn: '2027-04-04',
      latitude: 41.9028,
      longitude: 12.4964,
    });
    expect(body.destinations).toHaveLength(5);

    request.flush(
      stop({
        destinations: trip.destinations.map((destination) =>
          destination.id === 'b2'
            ? {
                ...destination,
                name: 'Kyoto, Japan',
                latitude: 35.0116,
                longitude: 135.7681,
              }
            : destination,
        ),
      }),
    );
    await settle();

    expect(rows()[1].textContent).toContain('Kyoto, Japan');
  });

  it('should never show a refused location as though it had been saved', async () => {
    await showTrip();

    rows()[1].click();
    fixture.detectChanges();

    button('Change location').click();
    fixture.detectChanges();
    await settle();

    const input = element().querySelector('.picker__input') as HTMLInputElement;
    input.value = 'Kyoto';
    input.dispatchEvent(new Event('input'));
    await settle(350);

    (element().querySelector('.picker__result') as HTMLButtonElement).click();
    fixture.detectChanges();
    button('Use this location').click();
    fixture.detectChanges();

    http
      .expectOne(`/api/trips/${trip.id}`)
      .flush(
        { title: 'Invalid trip', status: 400, detail: 'The trip destination name is required.' },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();

    expect(element().textContent).toContain('The trip destination name is required.');
    expect(element().textContent).toContain('The location was not changed.');

    // The stop keeps the name it was saved with.
    expect(rows()[1].textContent).toContain('Amalfi');
  });

  it('should show a not found state for an unknown trip', () => {
    createComponent('99999999-9999-9999-9999-999999999999');

    http
      .expectOne('/api/trips/99999999-9999-9999-9999-999999999999')
      .flush({ title: 'Trip not found', status: 404 }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(element().textContent).toContain('That trip could not be found');
  });

  it('should show an error state and try again', async () => {
    createComponent(trip.id);

    http
      .expectOne(`/api/trips/${trip.id}`)
      .flush({ title: 'Server error', status: 500 }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(element().textContent).toContain('Server error');

    element().querySelector('.empty-state button')?.dispatchEvent(new Event('click'));
    fixture.detectChanges();

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    await settle();

    expect(element().textContent).toContain('Italy & Iceland');
  });

  it('should link back to the trip', async () => {
    await showTrip();

    const back = [...element().querySelectorAll('a')].find((anchor) =>
      anchor.textContent?.includes('Back to the trip'),
    ) as HTMLAnchorElement;

    expect(back.getAttribute('href')).toBe(`/trips/${trip.id}`);
  });

  it('should point at the atlas from the trip page', async () => {
    await showTrip();

    const atlas = [...element().querySelectorAll('a')].find((anchor) =>
      anchor.textContent?.includes('Open the trip'),
    ) as HTMLAnchorElement;

    expect(atlas.getAttribute('href')).toBe(`/trips/${trip.id}`);
  });

  it('should leave the page usable when the map cannot be drawn', async () => {
    engine.unavailable = true;

    await showTrip();

    expect(element().textContent).toContain('The map could not be drawn');
    expect(rows()).toHaveLength(5);
  });
});
