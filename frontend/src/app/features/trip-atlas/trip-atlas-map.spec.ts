import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TripDestination, TripStop } from '../../core/models/trip';
import {
  AtlasHandle,
  AtlasPoint,
  AtlasSelection,
  AtlasStopPoint,
  LocatedPoint,
  MapEngine,
} from '../../shared/map/map-engine';
import { TripAtlasMap } from './trip-atlas-map';

class FakeHandle implements AtlasHandle {
  destinations: AtlasPoint[][] = [];
  stops: AtlasStopPoint[][] = [];
  routes: AtlasPoint[][] = [];
  stopRoutes: LocatedPoint[][] = [];
  selections: (AtlasSelection | null)[] = [];
  revealed: (AtlasSelection | null)[] = [];
  framed = 0;
  resized = 0;
  destroyed = false;

  setDestinations(points: readonly AtlasPoint[]): void {
    this.destinations.push([...points]);
  }

  setStops(points: readonly AtlasStopPoint[]): void {
    this.stops.push([...points]);
  }

  setRoute(points: readonly AtlasPoint[]): void {
    this.routes.push([...points]);
  }

  setStopRoute(points: readonly LocatedPoint[]): void {
    this.stopRoutes.push([...points]);
  }

  setSelection(selection: AtlasSelection | null): void {
    this.selections.push(selection);
  }

  reveal(selection: AtlasSelection | null): void {
    this.revealed.push(selection);
  }

  fitToTrip(): void {
    this.framed += 1;
  }

  resize(): void {
    this.resized += 1;
  }

  onSelect(): void {}

  onError(): void {}

  destroy(): void {
    this.destroyed = true;
  }
}

class FakeMapEngine {
  readonly handle = new FakeHandle();

  async createAtlas(): Promise<AtlasHandle> {
    return this.handle;
  }
}

function destination(
  id: string,
  sortOrder: number,
  latitude: number | null,
  longitude: number | null,
): TripDestination {
  return {
    id,
    name: id,
    sortOrder,
    startsOn: null,
    endsOn: null,
    latitude,
    longitude,
    stops: [],
  };
}

function stop(id: string, latitude: number | null, longitude: number | null): TripStop {
  return {
    id,
    name: id,
    sortOrder: 0,
    startsOn: null,
    endsOn: null,
    latitude,
    longitude,
  };
}

describe('TripAtlasMap', () => {
  let fixture: ComponentFixture<TripAtlasMap>;
  let engine: FakeMapEngine;
  let handle: FakeHandle;

  beforeEach(async () => {
    engine = new FakeMapEngine();
    handle = engine.handle;

    await TestBed.configureTestingModule({
      imports: [TripAtlasMap],
      providers: [{ provide: MapEngine, useValue: engine }],
    }).compileComponents();

    fixture = TestBed.createComponent(TripAtlasMap);
    fixture.componentRef.setInput('label', 'Japan');
  });

  async function show(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('labels each located destination by its place on the whole route', async () => {
    fixture.componentRef.setInput('destinations', [
      destination('kyoto', 0, 35, 135),
      destination('nowhere', 1, null, null),
      destination('tokyo', 2, 35.6, 139.7),
    ]);

    await show();

    expect(handle.destinations.at(-1)?.map((point) => point.label)).toEqual(['1', '3']);
  });

  it('labels stops with their destination number and their own suffix', async () => {
    fixture.componentRef.setInput('parentPosition', 2);
    fixture.componentRef.setInput('stops', [
      stop('gion', 35, 135),
      stop('arashiyama', null, null),
      stop('nishiki', 35.1, 135.1),
    ]);

    await show();

    expect(handle.stops.at(-1)?.map((point) => point.label)).toEqual(['2a', '2c']);
  });

  it('connects only the located stops, in their own order', async () => {
    fixture.componentRef.setInput('parentPosition', 1);
    fixture.componentRef.setInput('stops', [
      stop('gion', 35, 135),
      stop('arashiyama', null, null),
      stop('nishiki', 35.1, 135.1),
    ]);

    await show();

    expect(handle.stopRoutes.at(-1)?.map((point) => point.id)).toEqual(['gion', 'nishiki']);
  });

  it('draws no stops and no stop line without a selected parent', async () => {
    fixture.componentRef.setInput('stops', [stop('gion', 35, 135)]);

    await show();

    expect(handle.stops.at(-1)).toEqual([]);
    expect(handle.stopRoutes.at(-1)).toEqual([]);
  });

  it('draws one located stop without a line of its own', async () => {
    fixture.componentRef.setInput('parentPosition', 1);
    fixture.componentRef.setInput('stops', [stop('gion', 35, 135)]);

    await show();

    expect(handle.stops.at(-1)?.map((point) => point.id)).toEqual(['gion']);
    expect(handle.stopRoutes.at(-1)?.map((point) => point.id)).toEqual(['gion']);
  });

  it('shares the selection and frames the trip', async () => {
    fixture.componentRef.setInput('selection', { kind: 'destination', id: 'kyoto' });
    fixture.componentRef.setInput('destinations', [destination('kyoto', 0, 35, 135)]);

    await show();
    fixture.componentInstance.frameTrip();

    expect(handle.selections.at(-1)).toEqual({ kind: 'destination', id: 'kyoto' });
    expect(handle.revealed.at(-1)).toEqual({ kind: 'destination', id: 'kyoto' });
    expect(handle.framed).toBeGreaterThan(0);
  });

  it('destroys the handle when the component goes away', async () => {
    await show();
    fixture.destroy();

    expect(handle.destroyed).toBe(true);
  });
});
