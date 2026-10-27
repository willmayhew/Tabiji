import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TripDestination, TripStop } from '../../core/models/trip';
import { TripAtlasPanel } from './trip-atlas-panel';

const gion: TripStop = {
  id: 'gion',
  name: 'Gion',
  sortOrder: 0,
  startsOn: '2027-04-02',
  endsOn: '2027-04-03',
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

describe('TripAtlasPanel', () => {
  let fixture: ComponentFixture<TripAtlasPanel>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TripAtlasPanel],
    }).compileComponents();

    fixture = TestBed.createComponent(TripAtlasPanel);
  });

  function show(inputs: {
    destination?: TripDestination | null;
    stop?: TripStop | null;
    parent?: TripDestination | null;
    place?: TripStop | null;
    position?: number | null;
    stopLabel?: string | null;
  }): void {
    fixture.componentRef.setInput('destination', inputs.destination ?? null);
    fixture.componentRef.setInput('stop', inputs.stop ?? null);
    fixture.componentRef.setInput('parent', inputs.parent ?? null);
    fixture.componentRef.setInput('place', inputs.place ?? null);
    fixture.componentRef.setInput('position', inputs.position ?? null);
    fixture.componentRef.setInput('total', 2);
    fixture.componentRef.setInput('destinations', [kyoto]);
    fixture.componentRef.setInput('stopLabel', inputs.stopLabel ?? null);
    fixture.detectChanges();
  }

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('shows a destination with its stops labelled hierarchically', () => {
    show({ destination: kyoto, position: 1 });

    const text = element().textContent ?? '';

    expect(text).toContain('Kyoto');
    expect(text).toContain('1a');
    expect(text).toContain('Gion');
  });

  it('selects a stop of the selected destination', () => {
    show({ destination: kyoto, position: 1 });
    const selected: TripStop[] = [];
    fixture.componentInstance.selectStop.subscribe((stop) => selected.push(stop));

    element().querySelector<HTMLButtonElement>('.atlas-panel__place--stop')?.click();

    expect(selected.map((stop) => stop.id)).toEqual(['gion']);
  });

  it('shows a stop with its place details and its hierarchical label', () => {
    show({ stop: gion, parent: kyoto, stopLabel: '1a' });

    const text = element().textContent ?? '';

    expect(text).toContain('Stop 1a at Kyoto');
    expect(text).toContain('Sightseeing');
    expect(text).toContain('Gion, Kyoto');
    expect(text).toContain('Go early.');
    expect(text).toContain('2 Apr');
    expect(text).toContain('35.0037, 135.7788');
  });

  it('edits and removes the selected stop', () => {
    show({ stop: gion, parent: kyoto, stopLabel: '1a' });
    const edited: TripStop[] = [];
    const removed: TripStop[] = [];
    fixture.componentInstance.editStop.subscribe((stop) => edited.push(stop));
    fixture.componentInstance.removeStop.subscribe((stop) => removed.push(stop));

    const buttons = element().querySelectorAll<HTMLButtonElement>('.atlas-panel__actions button');
    buttons[0].click();
    buttons[1].click();

    expect(edited.map((stop) => stop.id)).toEqual(['gion']);
    expect(removed.map((stop) => stop.id)).toEqual(['gion']);
  });

  it('shows a legacy unfiled place as not yet in a destination', () => {
    show({ place: unfiled });

    const text = element().textContent ?? '';

    expect(text).toContain('Not yet in a destination');
    expect(text).toContain('Someday');
    expect(text).toContain('Food');
    expect(text).toContain('Somewhere');
    expect(text).toContain('One day.');
    expect(text).toContain('File this place under a destination');
  });

  it('says what to select when nothing is', () => {
    show({});

    expect(element().textContent).toContain(
      'Select a destination, a stop or a place to see its details.',
    );
  });
});
