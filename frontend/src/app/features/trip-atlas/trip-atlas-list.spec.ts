import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TripDestination, TripStop } from '../../core/models/trip';
import { AtlasSelection } from '../../shared/map/map-engine';
import { TripAtlasList } from './trip-atlas-list';

function stop(
  id: string,
  sortOrder: number,
  overrides: Partial<TripStop> = {},
): TripStop {
  return {
    id,
    name: id,
    sortOrder,
    startsOn: null,
    endsOn: null,
    latitude: null,
    longitude: null,
    ...overrides,
  };
}

const gion = stop('Gion', 0, { latitude: 35, longitude: 135 });
const arashiyama = stop('Arashiyama', 1, { plannedOn: '2027-04-02' });

const kyoto: TripDestination = {
  id: 'kyoto',
  name: 'Kyoto',
  sortOrder: 0,
  startsOn: '2027-04-01',
  endsOn: '2027-04-04',
  latitude: 35,
  longitude: 135,
  stops: [gion, arashiyama],
};

const osaka: TripDestination = {
  id: 'osaka',
  name: 'Osaka',
  sortOrder: 1,
  startsOn: '2027-04-05',
  endsOn: '2027-04-07',
  latitude: 34.7,
  longitude: 135.5,
  stops: [],
};

const unfiled = stop('Someday', 0, { category: 'Sight' });

describe('TripAtlasList', () => {
  let fixture: ComponentFixture<TripAtlasList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TripAtlasList],
    }).compileComponents();

    fixture = TestBed.createComponent(TripAtlasList);
  });

  function show(
    options: {
      destinations?: TripDestination[];
      unfiledPlaces?: TripStop[];
      expanded?: Set<string>;
      selection?: AtlasSelection | null;
    } = {},
  ): void {
    fixture.componentRef.setInput('destinations', options.destinations ?? [kyoto, osaka]);
    fixture.componentRef.setInput('unfiledPlaces', options.unfiledPlaces ?? []);
    fixture.componentRef.setInput('startsOn', '2027-04-01');
    fixture.componentRef.setInput('endsOn', '2027-04-07');
    fixture.componentRef.setInput('expanded', options.expanded ?? new Set<string>());
    fixture.componentRef.setInput('selection', options.selection ?? null);
    fixture.detectChanges();
  }

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('adds a parent destination from the plus beside the Route heading', () => {
    show();
    const emitted: void[] = [];
    fixture.componentInstance.addDestination.subscribe(() => emitted.push(undefined));

    const plus = element().querySelector<HTMLButtonElement>('.atlas-list__plus');
    expect(plus?.getAttribute('aria-label')).toBe('Add destination');

    plus?.click();

    expect(emitted).toHaveLength(1);
  });

  it('adds a stop from the plus beside a destination stop count, with that destination', () => {
    show();
    const emitted: TripDestination[] = [];
    fixture.componentInstance.addStop.subscribe((destination) => emitted.push(destination));

    const pluses = element().querySelectorAll<HTMLButtonElement>('.atlas-list__plus');
    expect(pluses[1].getAttribute('aria-label')).toBe('Add a stop in Kyoto');

    pluses[1].click();

    expect(emitted.map((destination) => destination.id)).toEqual(['kyoto']);
  });

  it('is a real button with an accessible expand/collapse label', () => {
    show();

    const chevrons = element().querySelectorAll<HTMLButtonElement>('.atlas-list__chevron');
    expect(chevrons).toHaveLength(2);
    expect(chevrons[0].tagName).toBe('BUTTON');
    expect(chevrons[0].getAttribute('aria-expanded')).toBe('false');
    expect(chevrons[0].getAttribute('aria-label')).toBe('Show stops in Kyoto');
    expect(chevrons[0].getAttribute('aria-controls')).toBe('atlas-stops-kyoto');
  });

  it('emits the destination to toggle and shows its stops when expanded', () => {
    show();
    const emitted: string[] = [];
    fixture.componentInstance.toggleDestination.subscribe((id) => emitted.push(id));

    element().querySelectorAll<HTMLButtonElement>('.atlas-list__chevron')[0].click();

    expect(emitted).toEqual(['kyoto']);

    show({ expanded: new Set(['kyoto']) });

    const panel = element().querySelector<HTMLElement>('#atlas-stops-kyoto');
    expect(panel?.hidden).toBe(false);
    expect(
      [...element().querySelectorAll('.atlas-stops__node')].map((node) => node.textContent?.trim()),
    ).toEqual(['1a', '1b']);
    expect(
      element().querySelectorAll<HTMLButtonElement>('.atlas-list__chevron')[0].getAttribute(
        'aria-expanded',
      ),
    ).toBe('true');
  });

  it('hides the stops of a collapsed destination and leaves another independent', () => {
    show({ expanded: new Set(['kyoto']) });

    const panels = element().querySelectorAll<HTMLElement>('.atlas-list__stops');
    expect(panels[0].hidden).toBe(false);
    expect(panels[1].hidden).toBe(true);

    const chevrons = element().querySelectorAll<HTMLButtonElement>('.atlas-list__chevron');
    expect(chevrons[0].getAttribute('aria-expanded')).toBe('true');
    expect(chevrons[1].getAttribute('aria-expanded')).toBe('false');
  });

  it('labels more than twenty-six stops without colliding', () => {
    const many = Array.from({ length: 27 }, (_, index) => stop(`Stop ${index}`, index));
    show({
      destinations: [{ ...kyoto, stops: many }],
      expanded: new Set(['kyoto']),
    });

    const labels = [
      ...element().querySelectorAll('.atlas-stops__node'),
    ].map((node) => node.textContent?.trim());

    expect(labels[0]).toBe('1a');
    expect(labels[25]).toBe('1z');
    expect(labels[26]).toBe('1aa');
    expect(new Set(labels).size).toBe(27);
  });

  it('selects a destination, a stop and an unfiled place', () => {
    show({ unfiledPlaces: [unfiled] });
    const selected: AtlasSelection[] = [];
    fixture.componentInstance.select.subscribe((selection) => selected.push(selection));

    element().querySelector<HTMLButtonElement>('.atlas-list__item')?.click();
    show({ unfiledPlaces: [unfiled], expanded: new Set(['kyoto']) });
    const stopButton = element().querySelector<HTMLButtonElement>('.atlas-list__item--stop');
    stopButton?.click();
    element()
      .querySelector<HTMLButtonElement>('.atlas-list__unfiled-section .atlas-places__item')
      ?.click();

    expect(selected).toEqual([
      { kind: 'destination', id: 'kyoto' },
      { kind: 'stop', id: 'Gion' },
      { kind: 'place', id: 'Someday' },
    ]);
  });

  it('reads the planned places by day', () => {
    show({ expanded: new Set(['kyoto']) });

    const text = element().textContent ?? '';

    expect(text).toContain('Plan by day');
    expect(text).toContain('Arashiyama');
    expect(text).toContain('Kyoto');
  });

  it('keeps a legacy unfiled place accessible and can file it under a destination', () => {
    show({ unfiledPlaces: [unfiled] });

    expect(element().textContent).toContain('Not yet in a destination');
    expect(element().textContent).toContain('Sightseeing');

    const select = element().querySelector<HTMLSelectElement>('.atlas-list__file-select');
    if (select !== null) {
      select.value = 'kyoto';
    }

    const emitted: { place: TripStop; parentId: string }[] = [];
    fixture.componentInstance.filePlace.subscribe((event) => emitted.push(event));

    const buttons = element().querySelectorAll<HTMLButtonElement>('.atlas-places__file button');
    buttons[0].click();

    expect(emitted).toEqual([{ place: unfiled, parentId: 'kyoto' }]);
  });
});
