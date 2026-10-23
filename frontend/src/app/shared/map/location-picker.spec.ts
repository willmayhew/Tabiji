import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { GeocodingService } from '../../core/api/geocoding.service';
import { PlaceSelection, PlaceSuggestion } from '../../core/models/place';
import { LocationPicker } from './location-picker';
import { MapEngine, MapHandle, MapOptions } from './map-engine';

const kyoto: PlaceSuggestion = {
  name: 'Kyoto',
  destinationName: 'Kyoto',
  label: 'Kyoto, Japan',
  latitude: 35.0116,
  longitude: 135.7681,
};

const asda: PlaceSuggestion = {
  name: 'Asda',
  destinationName: 'Asda, Poole',
  label: 'Asda, Poole, England, United Kingdom',
  latitude: 50.7182,
  longitude: -1.9868,
};

class FakeGeocodingService {
  search = vi.fn<(query: string) => Observable<PlaceSuggestion[]>>(() => of([kyoto]));
  reverse = vi.fn<(latitude: number, longitude: number) => Observable<PlaceSuggestion | null>>(() =>
    of(kyoto),
  );
}

class FakeMapEngine {
  private clickHandler: ((latitude: number, longitude: number) => void) | null = null;

  handle: MapHandle = {
    setMarker: vi.fn(),
    setCentre: vi.fn(),
    onClick: vi.fn((handler: (latitude: number, longitude: number) => void) => {
      this.clickHandler = handler;
    }),
    destroy: vi.fn(),
  };

  create = vi.fn<(container: HTMLElement, options: MapOptions) => Promise<MapHandle | null>>(
    async () => this.handle,
  );

  /** Simulates the reader clicking a point on the map. */
  click(latitude: number, longitude: number): void {
    this.clickHandler?.(latitude, longitude);
  }
}

describe('LocationPicker', () => {
  let fixture: ComponentFixture<LocationPicker>;
  let geocoding: FakeGeocodingService;
  let mapEngine: FakeMapEngine;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    geocoding = new FakeGeocodingService();
    mapEngine = new FakeMapEngine();

    await TestBed.configureTestingModule({
      imports: [LocationPicker],
      providers: [
        { provide: GeocodingService, useValue: geocoding },
        { provide: MapEngine, useValue: mapEngine },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LocationPicker);
    fixture.detectChanges();
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function dialog(): HTMLDialogElement {
    return element().querySelector('dialog') as HTMLDialogElement;
  }

  function button(text: string): HTMLButtonElement {
    const found = [...element().querySelectorAll('button')].find((candidate) =>
      candidate.textContent?.includes(text),
    );

    return found as HTMLButtonElement;
  }

  function open(inputs: { query?: string; coordinates?: PlaceSelection | null } = {}): void {
    if (inputs.query !== undefined) {
      fixture.componentRef.setInput('initialQuery', inputs.query);
    }

    if (inputs.coordinates !== undefined) {
      fixture.componentRef.setInput('initialCoordinates', inputs.coordinates);
    }

    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
  }

  function type(value: string): void {
    const input = element().querySelector('.picker__input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  /** Lets the queued timers run, including the search debounce. */
  async function settle(milliseconds = 0): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, milliseconds));
    fixture.detectChanges();
  }

  it('should stay closed until it is opened', () => {
    expect(dialog().hasAttribute('open')).toBe(false);
  });

  it('should open as a dialog and put the focus in the search box', async () => {
    open();
    await settle();

    expect(dialog().hasAttribute('open')).toBe(true);
    expect(document.activeElement).toBe(element().querySelector('.picker__input'));
  });

  it('should seed the search with the name the destination already has', () => {
    open({ query: 'Kyoto' });

    expect((element().querySelector('.picker__input') as HTMLInputElement).value).toBe('Kyoto');
  });

  it('should show the places the search finds', async () => {
    open();
    type('ky');
    await settle(350);

    const results = [...element().querySelectorAll('.picker__result')].map((node) =>
      node.textContent?.trim(),
    );

    expect(geocoding.search).toHaveBeenCalledWith('ky');
    expect(results).toHaveLength(1);
    expect(results[0]).toContain('Kyoto');
  });

  it('should wait for the reader to settle before asking the geocoder', async () => {
    open();
    type('k');

    expect(geocoding.search).not.toHaveBeenCalled();

    await settle(350);

    expect(geocoding.search).toHaveBeenCalledTimes(1);
  });

  it('should not ask the geocoder for an empty query', async () => {
    open();
    type('   ');
    await settle(350);

    expect(geocoding.search).not.toHaveBeenCalled();
    expect(element().querySelectorAll('.picker__result')).toHaveLength(0);
  });

  it('should say when nothing matches', async () => {
    geocoding.search.mockReturnValue(of([]));
    open();
    type('zzz');
    await settle(350);

    expect(element().textContent).toContain('No places found');
  });

  it('should leave typing usable when the search fails', async () => {
    geocoding.search.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 503,
            statusText: 'Service Unavailable',
            error: { title: 'Search is unavailable' },
          }),
      ),
    );
    open();
    type('kyoto');
    await settle(350);

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'Search is unavailable',
    );
    expect(element().textContent).toContain('type the name by hand');
  });

  it('should keep the confirm action disabled until a place is chosen', () => {
    open();

    expect(button('Use this location').disabled).toBe(true);
  });

  it('should hand the chosen place back when the reader confirms', async () => {
    open();
    const chosen: PlaceSelection[] = [];
    fixture.componentInstance.chosen.subscribe((place) => chosen.push(place));

    type('ky');
    await settle(350);

    (element().querySelector('.picker__result') as HTMLButtonElement).click();
    fixture.detectChanges();
    button('Use this location').click();

    expect(chosen).toEqual([{ name: 'Kyoto', latitude: 35.0116, longitude: 135.7681 }]);
  });

  it('should fill a destination with the name and its locality', async () => {
    geocoding.search.mockReturnValue(of([asda]));
    open();
    const chosen: PlaceSelection[] = [];
    fixture.componentInstance.chosen.subscribe((place) => chosen.push(place));

    type('asda');
    await settle(350);

    (element().querySelector('.picker__result') as HTMLButtonElement).click();
    fixture.detectChanges();
    button('Use this location').click();

    expect(chosen).toEqual([{ name: 'Asda, Poole', latitude: 50.7182, longitude: -1.9868 }]);
  });

  it('should change nothing when the reader cancels', () => {
    open();
    const dismissed: unknown[] = [];
    const chosen: PlaceSelection[] = [];
    fixture.componentInstance.dismissed.subscribe(() => dismissed.push(true));
    fixture.componentInstance.chosen.subscribe((place) => chosen.push(place));

    button('Cancel').click();

    expect(dismissed).toHaveLength(1);
    expect(chosen).toEqual([]);
  });

  it('should close on Escape', () => {
    open();
    const dismissed: unknown[] = [];
    fixture.componentInstance.dismissed.subscribe(() => dismissed.push(true));

    dialog().dispatchEvent(new Event('cancel'));

    expect(dismissed).toHaveLength(1);
  });

  it('should name a point clicked on the map', async () => {
    open();
    await settle();

    mapEngine.click(35.0116, 135.7681);
    fixture.detectChanges();

    expect(geocoding.reverse).toHaveBeenCalledWith(35.0116, 135.7681);
    expect(element().querySelector('.picker__selection-name')?.textContent).toContain('Kyoto');
  });

  it('should show a point clicked on the map even when it names no place', async () => {
    geocoding.reverse.mockReturnValue(of(null));
    open();
    await settle();

    mapEngine.click(35.0116, 135.7681);
    fixture.detectChanges();

    expect(element().querySelector('.picker__selection-coords')?.textContent).toContain('35.0116');
  });

  it('should carry on with search alone when the map cannot be drawn', async () => {
    mapEngine.create.mockResolvedValue(null);
    open();
    await settle();

    expect(element().querySelector('.picker__map--unavailable')).not.toBeNull();
    expect(element().textContent).toContain('The map could not be drawn');
  });

  it('should mark a destination that already has coordinates', async () => {
    open({
      query: 'Kyoto',
      coordinates: { name: 'Kyoto', latitude: 35.0116, longitude: 135.7681 },
    });
    await settle();

    expect(element().querySelector('.picker__selection-name')?.textContent).toContain('Kyoto');
    expect(mapEngine.handle.setMarker).toHaveBeenCalledWith(35.0116, 135.7681);
  });

  it('should give the focus back to where it came from when it closes', async () => {
    const trigger = document.createElement('button');
    document.body.appendChild(trigger);
    trigger.focus();

    open();
    await settle();

    fixture.componentRef.setInput('open', false);
    fixture.detectChanges();
    await settle();

    expect(document.activeElement).toBe(trigger);

    trigger.remove();
  });
});
