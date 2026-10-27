import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { convertToParamMap, provideRouter } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { Observable, of } from 'rxjs';
import { Trip, TripDestination, TripStop, TripStopUpdateDraft, UpdateTrip } from '../../core/models/trip';
import { TripsService } from '../../core/api/trips.service';
import {
  AtlasHandle,
  AtlasPoint,
  AtlasSelection,
  AtlasStopPoint,
  LocatedPoint,
  MapEngine,
} from '../../shared/map/map-engine';
import { DestinationEditor, NewPlaceDraft } from './destination-editor';
import { StopEditor, StopDraft } from './stop-editor';
import { TripAtlas } from './trip-atlas';
import { TripAtlasPanel } from './trip-atlas-panel';

const gion: TripStop = {
  id: 'gion',
  name: 'Gion',
  sortOrder: 0,
  startsOn: null,
  endsOn: null,
  latitude: 35.0037,
  longitude: 135.7788,
  category: 'Sight',
  address: 'Gion, Kyoto',
  notes: 'Go early.',
  plannedOn: '2027-04-02',
};

const kyoto: TripDestination = {
  id: 'kyoto',
  name: 'Kyoto',
  sortOrder: 0,
  startsOn: '2027-04-01',
  endsOn: '2027-04-04',
  latitude: 35,
  longitude: 135,
  stops: [gion],
};

const osaka: TripDestination = {
  id: 'osaka',
  name: 'Osaka',
  sortOrder: 1,
  startsOn: '2027-04-05',
  endsOn: '2027-04-10',
  latitude: 34.7,
  longitude: 135.5,
  stops: [],
};

const unfiled: TripStop = {
  id: 'unfiled',
  name: 'Someday',
  sortOrder: 0,
  startsOn: null,
  endsOn: null,
  latitude: 35,
  longitude: 135,
  category: 'Food',
  address: 'Somewhere',
  notes: 'One day.',
  plannedOn: null,
};

const trip: Trip = {
  id: 'trip-1',
  name: 'Japan',
  destinations: [kyoto, osaka],
  startsOn: '2027-04-01',
  endsOn: '2027-04-10',
  status: 'Planning',
  unfiledPlaces: [unfiled],
};

function fromUpdate(body: UpdateTrip): Trip {
  let next = 100;

  const stopFromDraft = (draft: TripStopUpdateDraft, sortOrder: number): TripStop => ({
    id: draft.id ?? `stop-${next++}`,
    name: draft.name,
    sortOrder,
    startsOn: draft.startsOn,
    endsOn: draft.endsOn,
    latitude: draft.latitude,
    longitude: draft.longitude,
    category: draft.category,
    address: draft.address,
    notes: draft.notes,
    plannedOn: draft.plannedOn,
  });

  return {
    id: 'trip-1',
    name: body.name,
    startsOn: body.startsOn,
    endsOn: body.endsOn,
    status: 'Planning',
    destinations: body.destinations.map((destination, index) => ({
      id: destination.id ?? `dest-${next++}`,
      name: destination.name,
      sortOrder: index,
      startsOn: destination.startsOn,
      endsOn: destination.endsOn,
      latitude: destination.latitude,
      longitude: destination.longitude,
      category: destination.category,
      address: destination.address,
      notes: destination.notes,
      plannedOn: destination.plannedOn,
      stops: destination.stops.map(stopFromDraft),
    })),
    unfiledPlaces: (body.unfiledPlaces ?? []).map((place, index) => ({
      id: place.id ?? `place-${next++}`,
      name: place.name,
      sortOrder: index,
      startsOn: place.startsOn,
      endsOn: place.endsOn,
      latitude: place.latitude,
      longitude: place.longitude,
      category: place.category,
      address: place.address,
      notes: place.notes,
      plannedOn: place.plannedOn,
    })),
  };
}

class FakeTripsService {
  readonly updates: UpdateTrip[] = [];

  getById(): Observable<Trip> {
    return of(trip);
  }

  update(_id: string, body: UpdateTrip): Observable<Trip> {
    this.updates.push(body);
    return of(fromUpdate(body));
  }
}

class FakeHandle implements AtlasHandle {
  destinations: AtlasPoint[][] = [];
  stops: AtlasStopPoint[][] = [];
  stopRoutes: LocatedPoint[][] = [];
  selections: (AtlasSelection | null)[] = [];

  setDestinations(points: readonly AtlasPoint[]): void {
    this.destinations.push([...points]);
  }

  setStops(points: readonly AtlasStopPoint[]): void {
    this.stops.push([...points]);
  }

  setRoute(): void {}

  setStopRoute(points: readonly LocatedPoint[]): void {
    this.stopRoutes.push([...points]);
  }

  setSelection(selection: AtlasSelection | null): void {
    this.selections.push(selection);
  }

  reveal(): void {}

  fitToTrip(): void {}

  resize(): void {}

  onSelect(): void {}

  onError(): void {}

  destroy(): void {}
}

class FakeMapEngine {
  readonly handle = new FakeHandle();

  async createAtlas(): Promise<AtlasHandle> {
    return this.handle;
  }
}

describe('TripAtlas', () => {
  let fixture: ComponentFixture<TripAtlas>;
  let trips: FakeTripsService;
  let map: FakeMapEngine;
  let handle: FakeHandle;

  beforeEach(async () => {
    trips = new FakeTripsService();
    map = new FakeMapEngine();
    handle = map.handle;

    await TestBed.configureTestingModule({
      imports: [TripAtlas],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: TripsService, useValue: trips },
        { provide: MapEngine, useValue: map },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ id: 'trip-1' })) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TripAtlas);
  });

  async function show(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function component(): TripAtlas {
    return fixture.componentInstance;
  }

  function list(): HTMLElement {
    return element().querySelector('.atlas-list') as HTMLElement;
  }

  function panel(): TripAtlasPanel {
    return fixture.debugElement.query(By.directive(TripAtlasPanel)).componentInstance;
  }

  function stopEditor(): StopEditor {
    return fixture.debugElement.query(By.directive(StopEditor)).componentInstance;
  }

  function destinationEditor(): DestinationEditor {
    return fixture.debugElement.query(By.directive(DestinationEditor)).componentInstance;
  }

  const draft: StopDraft = {
    name: 'Fushimi Inari',
    startsOn: null,
    endsOn: null,
    latitude: 34.9671,
    longitude: 135.7727,
    category: 'Sight',
    address: 'Fushimi Inari Taisha',
    notes: 'Many gates.',
    plannedOn: '2027-04-03',
  };

  it('loads the trip and draws only the main route until a destination is opened', async () => {
    await show();

    expect(handle.destinations.at(-1)?.map((point) => point.label)).toEqual(['1', '2']);
    expect(handle.stops.at(-1)).toEqual([]);
    expect(handle.stopRoutes.at(-1)).toEqual([]);
  });

  it('adds a parent destination from the Route plus', async () => {
    await show();

    list().querySelector<HTMLButtonElement>('.atlas-list__plus')?.click();
    fixture.detectChanges();

    const newParent: NewPlaceDraft = {
      name: 'Nara',
      startsOn: '2027-04-08',
      endsOn: '2027-04-10',
      latitude: null,
      longitude: null,
    };

    destinationEditor().save.emit(newParent);
    await show();

    const body = trips.updates[0];
    expect(body.destinations.map((destination) => destination.name)).toEqual([
      'Kyoto',
      'Osaka',
      'Nara',
    ]);
    expect(body.unfiledPlaces?.map((place) => place.id)).toEqual(['unfiled']);
    expect(component().selection()?.kind).toBe('destination');
  });

  it('adds a stop from the plus beside the destination stop count', async () => {
    await show();

    const pluses = list().querySelectorAll<HTMLButtonElement>('.atlas-list__plus');
    pluses[1].click();
    fixture.detectChanges();

    expect(component().stopEditorParent()?.id).toBe('kyoto');
    expect(component().expanded().has('kyoto')).toBe(true);

    stopEditor().save.emit(draft);
    await show();

    const body = trips.updates[0];
    const kyotoDraft = body.destinations.find((destination) => destination.id === 'kyoto');
    expect(kyotoDraft?.stops.map((stop) => stop.name)).toEqual(['Gion', 'Fushimi Inari']);

    const created = kyotoDraft?.stops[1];
    expect(created?.category).toBe('Sight');
    expect(created?.address).toBe('Fushimi Inari Taisha');
    expect(created?.notes).toBe('Many gates.');
    expect(created?.plannedOn).toBe('2027-04-03');
    expect(component().selection()?.kind).toBe('stop');
  });

  it('edits the selected stop and keeps its identity', async () => {
    await show();
    component().selection.set({ kind: 'stop', id: 'gion' });
    fixture.detectChanges();

    panel().editStop.emit(gion);
    fixture.detectChanges();

    stopEditor().save.emit({ ...draft, name: 'Gion at night', plannedOn: null });
    await show();

    const body = trips.updates[0];
    const kyotoDraft = body.destinations.find((destination) => destination.id === 'kyoto');
    const edited = kyotoDraft?.stops.find((stop) => stop.id === 'gion');

    expect(edited?.name).toBe('Gion at night');
    expect(edited?.plannedOn).toBeNull();
    expect(kyotoDraft?.stops.map((stop) => stop.id)).toEqual(['gion']);
  });

  it('removes the selected stop after confirmation and returns to its destination', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);

    await show();
    component().selection.set({ kind: 'stop', id: 'gion' });
    fixture.detectChanges();

    panel().removeStop.emit(gion);
    await show();

    const body = trips.updates[0];
    const kyotoDraft = body.destinations.find((destination) => destination.id === 'kyoto');
    expect(kyotoDraft?.stops).toEqual([]);
    expect(component().selection()).toEqual({ kind: 'destination', id: 'kyoto' });
    expect(confirm).toHaveBeenCalledWith(expect.stringContaining('Gion'));

    confirm.mockRestore();
  });

  it('shares the expansion state with the map and clears the stop line on collapse', async () => {
    await show();
    component().expanded.set(new Set(['kyoto']));
    component().selection.set({ kind: 'destination', id: 'kyoto' });
    await show();

    expect(handle.stops.at(-1)?.map((point) => point.id)).toEqual(['gion']);
    expect(handle.stopRoutes.at(-1)?.map((point) => point.id)).toEqual(['gion']);

    list().querySelector<HTMLButtonElement>('.atlas-list__chevron')?.click();
    await show();

    expect(component().expanded().has('kyoto')).toBe(false);
    expect(handle.stops.at(-1)).toEqual([]);
    expect(handle.stopRoutes.at(-1)).toEqual([]);
  });

  it('shows the hierarchical label of a stop in the list and the panel', async () => {
    await show();
    component().expanded.set(new Set(['kyoto']));
    component().selection.set({ kind: 'stop', id: 'gion' });
    await show();

    expect(
      list().querySelector('.atlas-stops__node')?.textContent?.trim(),
    ).toBe('1a');
    expect(element().textContent).toContain('Stop 1a at Kyoto');
  });

  it('files a legacy unfiled place under a destination without duplicating it', async () => {
    await show();

    const select = list().querySelector<HTMLSelectElement>('.atlas-list__file-select');
    expect(select).not.toBeNull();
    select!.value = 'kyoto';

    list()
      .querySelector<HTMLButtonElement>('.atlas-places__file button')
      ?.click();
    await show();

    const body = trips.updates[0];
    const kyotoDraft = body.destinations.find((destination) => destination.id === 'kyoto');
    const filed = kyotoDraft?.stops.find((stop) => stop.id === 'unfiled');

    expect(filed?.name).toBe('Someday');
    expect(filed?.category).toBe('Food');
    expect(body.unfiledPlaces).toEqual([]);
    expect(component().selection()).toEqual({ kind: 'stop', id: 'unfiled' });
  });

  it('turns a selected stop into its destination when the destination is collapsed', async () => {
    await show();
    component().expanded.set(new Set(['kyoto']));
    component().selection.set({ kind: 'stop', id: 'gion' });
    await show();

    list().querySelector<HTMLButtonElement>('.atlas-list__chevron')?.click();
    await show();

    expect(component().selection()).toEqual({ kind: 'destination', id: 'kyoto' });
  });

  it('returns to the overview when the selected destination is chosen again', async () => {
    await show();

    const destination = () => list().querySelector<HTMLButtonElement>('.atlas-list__item');

    destination()?.click();
    await show();

    expect(component().selection()).toEqual({ kind: 'destination', id: 'kyoto' });
    expect(element().textContent).not.toContain('Back to overview');

    destination()?.click();
    await show();

    expect(component().selection()).toBeNull();
    expect(element().textContent).toContain(
      'Select a destination, a stop or a place to see its details.',
    );
  });

  it('returns to the overview when the selected stop is chosen again', async () => {
    await show();
    component().expanded.set(new Set(['kyoto']));
    await show();

    const stop = () => list().querySelector<HTMLButtonElement>('.atlas-list__item--stop');

    stop()?.click();
    await show();

    expect(component().selection()).toEqual({ kind: 'stop', id: 'gion' });

    stop()?.click();
    await show();

    expect(component().selection()).toBeNull();
  });
});
