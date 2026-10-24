import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { GeocodingService } from '../../core/api/geocoding.service';
import { PlaceSuggestion, SavedPlace, SavedPlaceDraft } from '../../core/models/place';
import { Trip } from '../../core/models/trip';
import {
  AtlasHandle,
  AtlasOptions,
  AtlasPoint,
  AtlasSelection,
  LocatedPoint,
  MapEngine,
} from '../../shared/map/map-engine';
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
      id: 'd4',
      name: 'Reykjavík',
      sortOrder: 2,
      startsOn: '2027-04-06',
      endsOn: '2027-04-09',
      latitude: 64.1466,
      longitude: -21.9426,
    },
  ],
  startsOn: '2027-04-01',
  endsOn: '2027-04-10',
  status: 'Planning',
};

const fushimi: SavedPlace = {
  id: 'p1',
  destinationId: 'a1',
  name: 'Fushimi Inari',
  latitude: 34.9671,
  longitude: 135.7727,
  category: 'Sight',
  address: '68 Fukakusa Yabunouchicho, Kyoto',
  notes: 'Go early.',
  createdAt: '2026-10-20T09:00:00+00:00',
};

const nishiki: SavedPlace = {
  id: 'p2',
  destinationId: null,
  name: 'Nishiki Market',
  latitude: 35.005,
  longitude: 135.7649,
  category: 'Food',
  address: null,
  notes: null,
  createdAt: '2026-10-20T10:00:00+00:00',
};

class FakeGeocodingService {
  search = (): Observable<PlaceSuggestion[]> => of([kyoto]);
  reverse = (): Observable<PlaceSuggestion | null> => of(null);
}

class FakeMapEngine {
  unavailable = false;
  fitted = 0;
  destroyed = 0;
  readonly drawn: AtlasPoint[][] = [];
  readonly placed: LocatedPoint[][] = [];
  readonly selected: (AtlasSelection | null)[] = [];
  selectHandler: ((selection: AtlasSelection) => void) | null = null;

  handle: AtlasHandle = {
    setDestinations: (points) => this.drawn.push([...points]),
    setPlaces: (points) => this.placed.push([...points]),
    setRoute: () => undefined,
    setSelection: (selection) => this.selected.push(selection),
    fitToTrip: () => {
      this.fitted += 1;
    },
    resize: () => undefined,
    onSelect: (handler) => {
      this.selectHandler = handler;
    },
    onError: () => undefined,
    destroy: () => {
      this.destroyed += 1;
    },
  };

  // The picker asks for a single-marker map; the atlas asks for the trip.
  create = () => Promise.resolve(null);

  createAtlas = (_container: HTMLElement, _options: AtlasOptions): Promise<AtlasHandle | null> =>
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

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function button(text: string): HTMLButtonElement {
    const found = [...element().querySelectorAll('button')].find((candidate) =>
      candidate.textContent?.includes(text),
    );

    return found as HTMLButtonElement;
  }

  function stopRows(): HTMLButtonElement[] {
    return [...element().querySelectorAll('.atlas-list__item')] as HTMLButtonElement[];
  }

  function placeRows(): HTMLButtonElement[] {
    return [...element().querySelectorAll('.atlas-places__item')] as HTMLButtonElement[];
  }

  function placeRow(name: string): HTMLButtonElement {
    return placeRows().find((row) => row.textContent?.includes(name)) as HTMLButtonElement;
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

  /** Loads the trip and its places, and leaves the page showing both. */
  async function showAtlas(options: { trip?: Trip; places?: SavedPlace[] } = {}): Promise<void> {
    createComponent(trip.id);

    http.expectOne(`/api/trips/${trip.id}`).flush(options.trip ?? trip);
    fixture.detectChanges();
    await fixture.whenStable();

    http.expectOne(`/api/trips/${trip.id}/places`).flush(options.places ?? []);
    await settle();
  }

  async function openEditor(): Promise<void> {
    button('Add a place').click();
    fixture.detectChanges();
    await settle();
  }

  async function chooseKyoto(): Promise<void> {
    const box = element().querySelector('.editor__search-input') as HTMLInputElement;
    box.value = 'kyoto';
    box.dispatchEvent(new Event('input'));
    await settle(350);

    (element().querySelector('.editor__result') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function submitEditor(): void {
    (element().querySelector('.editor form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  it('should show a loading state while the trip is fetched', () => {
    createComponent(trip.id);

    expect(element().textContent).toContain('Loading the atlas');

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();

    // The places are asked for once the trip, and so its id, is known.
    http.expectOne(`/api/trips/${trip.id}/places`).flush([]);
  });

  it('should show the trip name as the only heading', async () => {
    await showAtlas();

    const headings = element().querySelectorAll('h1');

    expect(headings).toHaveLength(1);
    expect(headings[0].textContent).toContain('Italy & Iceland');
  });

  it('should list every stop, including the ones with no location', async () => {
    await showAtlas();

    expect(stopRows()).toHaveLength(3);
    expect(element().textContent).toContain('Amalfi');
  });

  it('should draw only the located stops on the map', async () => {
    await showAtlas();

    expect(engine.drawn.at(-1)?.map((point) => point.id)).toEqual(['a1', 'd4']);
  });

  it('should show an empty map state when there is nothing to draw', async () => {
    await showAtlas({
      trip: {
        ...trip,
        destinations: trip.destinations.map((destination) => ({
          ...destination,
          latitude: null,
          longitude: null,
        })),
      },
    });

    expect(element().textContent).toContain('Nothing on the map yet');
    expect(element().querySelector('app-trip-atlas-map')).toBeNull();
  });

  it('should own up to a route that is only partly on the map', async () => {
    await showAtlas();

    expect(element().textContent).toContain('2 of 3 stops are on the map');
  });

  it('should load the places saved to the trip', async () => {
    await showAtlas({ places: [fushimi, nishiki] });

    expect(placeRows()).toHaveLength(2);
    expect(placeRow('Fushimi Inari').textContent).toContain('Sight');
    expect(placeRow('Fushimi Inari').textContent).toContain('Near Rome');
    expect(placeRow('Nishiki Market').textContent).toContain('Food');
  });

  it('should draw the saved places on the map', async () => {
    await showAtlas({ places: [fushimi] });

    expect(engine.placed.at(-1)?.map((point) => point.id)).toEqual(['p1']);
  });

  it('should say when nothing is saved yet', async () => {
    await showAtlas();

    expect(element().textContent).toContain('Nothing saved yet');
  });

  it('should keep the route usable when the places cannot be loaded, and try again', async () => {
    createComponent(trip.id);
    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();
    await fixture.whenStable();

    http.expectOne(`/api/trips/${trip.id}/places`).flush(
      { title: 'Server error', status: 500 },
      { status: 500, statusText: 'Server Error' },
    );
    await settle();

    // The trip, the route and the map are all still there.
    expect(element().textContent).toContain('Server error');
    expect(stopRows()).toHaveLength(3);

    button('Try again').click();
    await settle();

    http.expectOne(`/api/trips/${trip.id}/places`).flush([fushimi]);
    await settle();

    expect(placeRows()).toHaveLength(1);
  });

  it('should select a stop from the list and show its details', async () => {
    await showAtlas();

    stopRows()[1].click();
    fixture.detectChanges();

    expect(stopRows()[1].getAttribute('aria-pressed')).toBe('true');
    expect(element().querySelector('.atlas-panel__name')?.textContent).toContain('Amalfi');
    expect(element().textContent).toContain('Stop 2 of 3');
  });

  it('should select a stop from the map', async () => {
    await showAtlas();

    engine.selectHandler?.({ kind: 'destination', id: 'd4' });
    fixture.detectChanges();

    const pressed = stopRows().filter((row) => row.getAttribute('aria-pressed') === 'true');

    expect(pressed).toHaveLength(1);
    expect(pressed[0].textContent).toContain('Reykjavík');
  });

  it('should select a saved place from the list and show its details', async () => {
    await showAtlas({ places: [fushimi, nishiki] });

    placeRow('Fushimi Inari').click();
    fixture.detectChanges();

    expect(placeRow('Fushimi Inari').getAttribute('aria-pressed')).toBe('true');
    expect(element().textContent).toContain('Saved place');
    expect(element().querySelector('.atlas-panel__name')?.textContent).toContain('Fushimi Inari');
    expect(element().textContent).toContain('68 Fukakusa Yabunouchicho, Kyoto');
    expect(element().textContent).toContain('Go early.');
    expect(element().textContent).toContain('Rome');
  });

  it('should select a saved place from the map', async () => {
    await showAtlas({ places: [fushimi, nishiki] });

    engine.selectHandler?.({ kind: 'place', id: 'p2' });
    fixture.detectChanges();

    const pressed = placeRows().filter((row) => row.getAttribute('aria-pressed') === 'true');

    expect(pressed).toHaveLength(1);
    expect(pressed[0].textContent).toContain('Nishiki Market');
    expect(element().querySelector('.atlas-panel__name')?.textContent).toContain('Nishiki Market');
  });

  it('should open the editor on an empty draft', async () => {
    await showAtlas();

    await openEditor();

    expect(element().querySelector('.editor')?.hasAttribute('open')).toBe(true);
    expect(element().querySelector('.editor__title')?.textContent).toContain('Add a place');
  });

  it('should save a place and only then show it', async () => {
    await showAtlas();

    await openEditor();
    await chooseKyoto();
    submitEditor();

    const request = http.expectOne(`/api/trips/${trip.id}/places`);

    expect(request.request.method).toBe('POST');

    const body = request.request.body as SavedPlaceDraft;

    expect(body.name).toBe('Kyoto');
    expect(body.latitude).toBe(35.0116);
    expect(body.longitude).toBe(135.7681);
    expect(body.address).toBe('Kyoto, Japan');
    expect(body.destinationId).toBeNull();

    // Nothing appears while the request is in flight.
    expect(placeRows()).toHaveLength(0);

    request.flush({
      id: 'p3',
      destinationId: null,
      name: 'Kyoto',
      latitude: 35.0116,
      longitude: 135.7681,
      category: null,
      address: 'Kyoto, Japan',
      notes: null,
      createdAt: '2026-10-20T11:00:00+00:00',
    });
    await settle();

    expect(placeRows()).toHaveLength(1);
    expect(placeRow('Kyoto').getAttribute('aria-pressed')).toBe('true');
    expect(engine.placed.at(-1)?.map((point) => point.id)).toEqual(['p3']);
    expect(element().querySelector('.editor')?.hasAttribute('open')).toBe(false);
  });

  it('should leave no phantom place when the save is refused', async () => {
    await showAtlas();

    await openEditor();
    await chooseKyoto();
    submitEditor();

    http
      .expectOne(`/api/trips/${trip.id}/places`)
      .flush(
        { title: 'Invalid saved place', status: 400, detail: 'The saved place name is required.' },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();

    expect(element().textContent).toContain('The saved place name is required.');
    expect(placeRows()).toHaveLength(0);
    expect(engine.placed.at(-1)).toEqual([]);

    // The editor stays open, holding what the reader wrote.
    expect(element().querySelector('.editor')?.hasAttribute('open')).toBe(true);
  });

  it('should change a saved place', async () => {
    await showAtlas({ places: [fushimi] });

    placeRow('Fushimi Inari').click();
    fixture.detectChanges();

    button('Edit place').click();
    fixture.detectChanges();
    await settle();

    const name = element().querySelector('#place-name') as HTMLInputElement;
    name.value = 'Fushimi Inari Taisha';
    name.dispatchEvent(new Event('input'));

    const notes = element().querySelector('#place-notes') as HTMLTextAreaElement;
    notes.value = '';
    notes.dispatchEvent(new Event('input'));

    submitEditor();

    const request = http.expectOne(`/api/trips/${trip.id}/places/p1`);

    expect(request.request.method).toBe('PUT');

    const body = request.request.body as SavedPlaceDraft;

    expect(body.name).toBe('Fushimi Inari Taisha');
    expect(body.latitude).toBe(34.9671);
    expect(body.longitude).toBe(135.7727);
    expect(body.notes).toBeNull();

    request.flush({ ...fushimi, name: 'Fushimi Inari Taisha', notes: null });
    await settle();

    expect(placeRow('Fushimi Inari Taisha')).toBeDefined();
    expect(element().querySelector('.atlas-panel__name')?.textContent).toContain(
      'Fushimi Inari Taisha',
    );
  });

  it('should take a place off the route when the association is cleared', async () => {
    await showAtlas({ places: [fushimi] });

    placeRow('Fushimi Inari').click();
    fixture.detectChanges();

    button('Edit place').click();
    fixture.detectChanges();
    await settle();

    const stop = element().querySelector('#place-destination') as HTMLSelectElement;
    stop.value = '';
    stop.dispatchEvent(new Event('change'));

    submitEditor();

    const request = http.expectOne(`/api/trips/${trip.id}/places/p1`);

    expect((request.request.body as SavedPlaceDraft).destinationId).toBeNull();

    request.flush({ ...fushimi, destinationId: null });
    await settle();

    expect(placeRow('Fushimi Inari').textContent).not.toContain('Near Rome');
  });

  it('should change nothing when an edit is cancelled', async () => {
    await showAtlas({ places: [fushimi] });

    placeRow('Fushimi Inari').click();
    fixture.detectChanges();

    button('Edit place').click();
    fixture.detectChanges();
    await settle();

    const name = element().querySelector('#place-name') as HTMLInputElement;
    name.value = 'Something else';
    name.dispatchEvent(new Event('input'));

    button('Cancel').click();
    fixture.detectChanges();
    await settle();

    http.expectNone(`/api/trips/${trip.id}/places/p1`);
    expect(placeRow('Fushimi Inari')).toBeDefined();
  });

  it('should remove a place once the reader has agreed', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    await showAtlas({ places: [fushimi, nishiki] });

    placeRow('Fushimi Inari').click();
    fixture.detectChanges();

    button('Delete place').click();
    await settle();

    expect(window.confirm).toHaveBeenCalled();

    const request = http.expectOne(`/api/trips/${trip.id}/places/p1`);
    expect(request.request.method).toBe('DELETE');

    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    expect(placeRows()).toHaveLength(1);
    expect(engine.placed.at(-1)?.map((point) => point.id)).toEqual(['p2']);
  });

  it('should remove nothing when the reader changes their mind', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    await showAtlas({ places: [fushimi] });

    placeRow('Fushimi Inari').click();
    fixture.detectChanges();

    button('Delete place').click();
    await settle();

    http.expectNone(`/api/trips/${trip.id}/places/p1`);
    expect(placeRows()).toHaveLength(1);
  });

  it('should keep a place when the removal fails', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    await showAtlas({ places: [fushimi] });

    placeRow('Fushimi Inari').click();
    fixture.detectChanges();

    button('Delete place').click();
    await settle();

    http
      .expectOne(`/api/trips/${trip.id}/places/p1`)
      .flush(
        { title: 'Server error', status: 500 },
        { status: 500, statusText: 'Server Error' },
      );
    await settle();

    expect(element().querySelector('[role="alert"]')?.textContent).toContain('Server error');
    expect(placeRows()).toHaveLength(1);
  });

  it('should keep the list usable when the map cannot be drawn', async () => {
    engine.unavailable = true;

    await showAtlas({ places: [fushimi] });

    expect(element().textContent).toContain('The map could not be drawn');
    expect(placeRows()).toHaveLength(1);
    expect(stopRows()).toHaveLength(3);
  });

  it('should frame the trip when it is asked to', async () => {
    await showAtlas();

    engine.fitted = 0;
    button('Frame the trip').click();

    expect(engine.fitted).toBe(1);
  });

  it('should take turns between the map and the list on a narrow screen', async () => {
    await showAtlas();

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
    await fixture.whenStable();

    http.expectOne(`/api/trips/${trip.id}/places`).flush([]);
    await settle();

    expect(element().textContent).toContain('Italy & Iceland');
  });

  it('should link back to the trip', async () => {
    await showAtlas();

    const back = [...element().querySelectorAll('a')].find((anchor) =>
      anchor.textContent?.includes('Back to the trip'),
    ) as HTMLAnchorElement;

    expect(back.getAttribute('href')).toBe(`/trips/${trip.id}`);
  });
});
