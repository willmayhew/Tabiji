import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SavedPlace } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import {
  AtlasHandle,
  AtlasOptions,
  AtlasPoint,
  AtlasSelection,
  LocatedPoint,
  MapEngine,
} from '../../shared/map/map-engine';
import { TripAtlasMap } from './trip-atlas-map';

const route: TripDestination[] = [
  {
    id: 'a1',
    name: 'Rome',
    sortOrder: 0,
    startsOn: null,
    endsOn: null,
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
    startsOn: null,
    endsOn: null,
    latitude: 64.1466,
    longitude: -21.9426,
  },
];

const places: SavedPlace[] = [
  {
    id: 'p1',
    destinationId: null,
    name: 'Fushimi Inari',
    latitude: 34.9671,
    longitude: 135.7727,
    category: 'Sight',
    address: null,
    notes: null,
    createdAt: '2026-10-20T09:00:00+00:00',
  },
];

class FakeMapEngine {
  readonly drawn: AtlasPoint[][] = [];
  readonly placed: LocatedPoint[][] = [];
  readonly routed: AtlasPoint[][] = [];
  readonly selected: (AtlasSelection | null)[] = [];
  readonly revealed: (AtlasSelection | null)[] = [];

  fitted = 0;
  resized = 0;
  destroyed = 0;
  options: AtlasOptions | null = null;
  unavailable = false;
  selectHandler: ((selection: AtlasSelection) => void) | null = null;
  errorHandler: (() => void) | null = null;

  handle: AtlasHandle = {
    setDestinations: (points) => this.drawn.push([...points]),
    setPlaces: (points) => this.placed.push([...points]),
    setRoute: (points) => this.routed.push([...points]),
    setSelection: (selection) => this.selected.push(selection),
    reveal: (selection) => this.revealed.push(selection),
    fitToTrip: () => {
      this.fitted += 1;
    },
    resize: () => {
      this.resized += 1;
    },
    onSelect: (handler) => {
      this.selectHandler = handler;
    },
    onError: (handler) => {
      this.errorHandler = handler;
    },
    destroy: () => {
      this.destroyed += 1;
    },
  };

  createAtlas = (container: HTMLElement, options: AtlasOptions): Promise<AtlasHandle | null> => {
    this.options = options;

    return Promise.resolve(this.unavailable ? null : this.handle);
  };
}

describe('TripAtlasMap', () => {
  let fixture: ComponentFixture<TripAtlasMap>;
  let engine: FakeMapEngine;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    engine = new FakeMapEngine();

    await TestBed.configureTestingModule({
      imports: [TripAtlasMap],
      providers: [{ provide: MapEngine, useValue: engine }],
    }).compileComponents();

    fixture = TestBed.createComponent(TripAtlasMap);
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  async function render(
    options: {
      destinations?: TripDestination[];
      places?: SavedPlace[];
      selection?: AtlasSelection | null;
    } = {},
  ): Promise<void> {
    fixture.componentRef.setInput('destinations', options.destinations ?? route);
    fixture.componentRef.setInput('places', options.places ?? []);
    fixture.componentRef.setInput('selection', options.selection ?? null);
    fixture.componentRef.setInput('label', 'Italy & Iceland');
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  it('should draw only the stops that have a location', async () => {
    await render();

    expect(engine.drawn.at(-1)?.map((point) => point.id)).toEqual(['a1', 'd4']);
  });

  it('should number a stop by its place on the whole route', async () => {
    await render();

    expect(engine.drawn.at(-1)?.map((point) => point.index)).toEqual([1, 4]);
  });

  it('should hand the coordinates over as they were given', async () => {
    await render();

    const drawn = engine.drawn.at(-1) ?? [];

    expect(drawn[0].latitude).toBe(41.9028);
    expect(drawn[0].longitude).toBe(12.4964);
    expect(drawn[1].latitude).toBe(64.1466);
  });

  it('should draw the route in the order the stops are visited', async () => {
    await render();

    expect(engine.routed.at(-1)?.map((point) => point.id)).toEqual(['a1', 'd4']);
  });

  it('should draw the saved places', async () => {
    await render({ places });

    expect(engine.placed.at(-1)).toEqual([
      { id: 'p1', latitude: 34.9671, longitude: 135.7727 },
    ]);
  });

  it('should keep the saved places out of the route', async () => {
    await render({ places });

    // A place is a point of interest, not a stop: it never joins the line.
    expect(engine.routed.at(-1)?.map((point) => point.id)).toEqual(['a1', 'd4']);
  });

  it('should tell the map when a place is added after it is drawn', async () => {
    await render();

    expect(engine.placed.at(-1)).toEqual([]);

    fixture.componentRef.setInput('places', places);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(engine.placed.at(-1)?.map((point) => point.id)).toEqual(['p1']);
  });

  it('should tell the map when a place goes', async () => {
    await render({ places });

    fixture.componentRef.setInput('places', []);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(engine.placed.at(-1)).toEqual([]);
  });

  it('should frame the trip once the map is drawn', async () => {
    await render();

    expect(engine.fitted).toBeGreaterThan(0);
  });

  it('should pass the tile style from the environment', async () => {
    await render();

    expect(engine.options?.styleUrl).toContain('https://');
  });

  it('should report a stop marker that was clicked', async () => {
    await render();

    const chosen: AtlasSelection[] = [];
    fixture.componentInstance.select.subscribe((selection) => chosen.push(selection));

    engine.selectHandler?.({ kind: 'destination', id: 'd4' });

    expect(chosen).toEqual([{ kind: 'destination', id: 'd4' }]);
  });

  it('should report a place marker that was clicked', async () => {
    await render({ places });

    const chosen: AtlasSelection[] = [];
    fixture.componentInstance.select.subscribe((selection) => chosen.push(selection));

    engine.selectHandler?.({ kind: 'place', id: 'p1' });

    expect(chosen).toEqual([{ kind: 'place', id: 'p1' }]);
  });

  it('should hand the selection to the map', async () => {
    await render({ selection: { kind: 'place', id: 'p1' } });

    expect(engine.selected).toContainEqual({ kind: 'place', id: 'p1' });
  });

  it('should tell the map when the selection changes after it is drawn', async () => {
    await render({ selection: null });

    engine.selected.length = 0;

    fixture.componentRef.setInput('selection', { kind: 'destination', id: 'd4' });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(engine.selected).toContainEqual({ kind: 'destination', id: 'd4' });
  });

  it('should ask the map to bring the selection into view', async () => {
    await render({ selection: null });

    engine.revealed.length = 0;

    fixture.componentRef.setInput('selection', { kind: 'place', id: 'p1' });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(engine.revealed).toContainEqual({ kind: 'place', id: 'p1' });
  });

  it('should ask the map to bring a selection made before it was drawn into view', async () => {
    await render({ selection: { kind: 'destination', id: 'a1' } });

    expect(engine.revealed).toContainEqual({ kind: 'destination', id: 'a1' });
  });

  it('should tell the map when the stops change after it is drawn', async () => {
    await render();

    const drawnBefore = engine.drawn.length;

    fixture.componentRef.setInput('destinations', route.slice(0, 1));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(engine.drawn.length).toBeGreaterThan(drawnBefore);
    expect(engine.drawn.at(-1)?.map((point) => point.id)).toEqual(['a1']);
  });

  it('should ask the map to frame the trip when it is asked to', async () => {
    await render();

    engine.fitted = 0;
    fixture.componentInstance.frameTrip();

    expect(engine.fitted).toBe(1);
  });

  it('should say so when the map cannot be drawn', async () => {
    engine.unavailable = true;
    await render();

    expect(element().textContent).toContain('The map could not be drawn');
  });

  it('should draw nothing when the map cannot be drawn', async () => {
    engine.unavailable = true;
    await render();

    expect(engine.drawn).toHaveLength(0);
  });

  it('should note quietly when something the map needed failed', async () => {
    await render();

    expect(element().textContent).not.toContain('could not be loaded');

    engine.errorHandler?.();
    fixture.detectChanges();

    expect(element().textContent).toContain('could not be loaded');
  });

  it('should re-measure a map that has returned to the screen', async () => {
    await render();

    fixture.componentRef.setInput('active', false);
    fixture.detectChanges();

    engine.resized = 0;

    fixture.componentRef.setInput('active', true);
    fixture.detectChanges();

    expect(engine.resized).toBeGreaterThan(0);
  });

  it('should let the map go when the component is destroyed', async () => {
    await render();

    fixture.destroy();

    expect(engine.destroyed).toBe(1);
  });

  it('should name the map for assistive technology', async () => {
    await render();

    expect(element().querySelector('.atlas-map__canvas')?.getAttribute('aria-label')).toBe(
      'Map of Italy & Iceland',
    );
  });
});
