import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TripDestination } from '../../core/models/trip';
import { AtlasHandle, AtlasOptions, AtlasPoint, MapEngine } from '../../shared/map/map-engine';
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

class FakeMapEngine {
  readonly drawn: AtlasPoint[][] = [];
  readonly routed: AtlasPoint[][] = [];
  readonly selected: (string | null)[] = [];

  fitted = 0;
  resized = 0;
  destroyed = 0;
  options: AtlasOptions | null = null;
  unavailable = false;
  selectHandler: ((id: string) => void) | null = null;
  errorHandler: (() => void) | null = null;

  handle: AtlasHandle = {
    setDestinations: (points) => this.drawn.push([...points]),
    setRoute: (points) => this.routed.push([...points]),
    setSelection: (id) => this.selected.push(id),
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
    destinations: TripDestination[],
    selectedId: string | null = null,
  ): Promise<void> {
    fixture.componentRef.setInput('destinations', destinations);
    fixture.componentRef.setInput('selectedId', selectedId);
    fixture.componentRef.setInput('label', 'Italy & Iceland');
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  it('should draw only the stops that have a location', async () => {
    await render(route);

    expect(engine.drawn.at(-1)?.map((point) => point.id)).toEqual(['a1', 'd4']);
  });

  it('should number a stop by its place on the whole route', async () => {
    await render(route);

    expect(engine.drawn.at(-1)?.map((point) => point.index)).toEqual([1, 4]);
  });

  it('should hand the coordinates over as they were given', async () => {
    await render(route);

    const drawn = engine.drawn.at(-1) ?? [];

    expect(drawn[0].latitude).toBe(41.9028);
    expect(drawn[0].longitude).toBe(12.4964);
    expect(drawn[1].latitude).toBe(64.1466);
  });

  it('should draw the route in the order the stops are visited', async () => {
    await render(route);

    expect(engine.routed.at(-1)?.map((point) => point.id)).toEqual(['a1', 'd4']);
  });

  it('should frame the trip once the map is drawn', async () => {
    await render(route);

    expect(engine.fitted).toBeGreaterThan(0);
  });

  it('should pass the tile style from the environment', async () => {
    await render(route);

    expect(engine.options?.styleUrl).toContain('https://');
  });

  it('should report the marker that was clicked', async () => {
    await render(route);

    const chosen: string[] = [];
    fixture.componentInstance.select.subscribe((id) => chosen.push(id));

    engine.selectHandler?.('d4');

    expect(chosen).toEqual(['d4']);
  });

  it('should hand the selection to the map', async () => {
    await render(route, 'a1');

    expect(engine.selected).toContain('a1');
  });

  it('should tell the map when the selection changes after it is drawn', async () => {
    await render(route, null);

    engine.selected.length = 0;

    fixture.componentRef.setInput('selectedId', 'd4');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(engine.selected).toContain('d4');
  });

  it('should tell the map when the stops change after it is drawn', async () => {
    await render(route);

    const drawnBefore = engine.drawn.length;

    fixture.componentRef.setInput('destinations', route.slice(0, 1));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(engine.drawn.length).toBeGreaterThan(drawnBefore);
    expect(engine.drawn.at(-1)?.map((point) => point.id)).toEqual(['a1']);
  });

  it('should ask the map to frame the trip when it is asked to', async () => {
    await render(route);

    engine.fitted = 0;
    fixture.componentInstance.frameTrip();

    expect(engine.fitted).toBe(1);
  });

  it('should say so when the map cannot be drawn', async () => {
    engine.unavailable = true;
    await render(route);

    expect(element().textContent).toContain('The map could not be drawn');
  });

  it('should draw nothing when the map cannot be drawn', async () => {
    engine.unavailable = true;
    await render(route);

    expect(engine.drawn).toHaveLength(0);
  });

  it('should note quietly when something the map needed failed', async () => {
    await render(route);

    expect(element().textContent).not.toContain('could not be loaded');

    engine.errorHandler?.();
    fixture.detectChanges();

    expect(element().textContent).toContain('could not be loaded');
  });

  it('should re-measure a map that has returned to the screen', async () => {
    await render(route);

    fixture.componentRef.setInput('active', false);
    fixture.detectChanges();

    engine.resized = 0;

    fixture.componentRef.setInput('active', true);
    fixture.detectChanges();

    expect(engine.resized).toBeGreaterThan(0);
  });

  it('should let the map go when the component is destroyed', async () => {
    await render(route);

    fixture.destroy();

    expect(engine.destroyed).toBe(1);
  });

  it('should name the map for assistive technology', async () => {
    await render(route);

    expect(element().querySelector('.atlas-map__canvas')?.getAttribute('aria-label')).toBe(
      'Map of Italy & Iceland',
    );
  });
});
