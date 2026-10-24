import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SavedPlace } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import { TripAtlasList } from './trip-atlas-list';

const route: TripDestination[] = [
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
];

const places: SavedPlace[] = [
  {
    id: 'p1',
    destinationId: 'a1',
    name: 'Fushimi Inari',
    latitude: 34.9671,
    longitude: 135.7727,
    category: 'Sight',
    address: '68 Fukakusa Yabunouchicho, Kyoto',
    notes: null,
    createdAt: '2026-10-20T09:00:00+00:00',
  },
  {
    id: 'p2',
    destinationId: null,
    name: 'Nishiki Market',
    latitude: 35.005,
    longitude: 135.7649,
    category: 'Food',
    address: null,
    notes: null,
    createdAt: '2026-10-20T10:00:00+00:00',
  },
];

describe('TripAtlasList', () => {
  let fixture: ComponentFixture<TripAtlasList>;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [TripAtlasList],
    }).compileComponents();

    fixture = TestBed.createComponent(TripAtlasList);
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function render(
    options: {
      destinations?: TripDestination[];
      places?: SavedPlace[];
      selection?: { kind: 'destination' | 'place'; id: string } | null;
      placesLoading?: boolean;
      placesError?: string | null;
    } = {},
  ): void {
    fixture.componentRef.setInput('destinations', options.destinations ?? route);
    fixture.componentRef.setInput('places', options.places ?? []);
    fixture.componentRef.setInput('selection', options.selection ?? null);
    fixture.componentRef.setInput('placesLoading', options.placesLoading ?? false);
    fixture.componentRef.setInput('placesError', options.placesError ?? null);
    fixture.detectChanges();
  }

  function stopRows(): HTMLButtonElement[] {
    return [...element().querySelectorAll('.atlas-list__item')] as HTMLButtonElement[];
  }

  function placeRows(): HTMLButtonElement[] {
    return [...element().querySelectorAll('.atlas-places__item')] as HTMLButtonElement[];
  }

  it('should list every stop, in order', () => {
    render();

    expect(stopRows().map((row) => row.querySelector('.atlas-list__name')?.textContent?.trim())).toEqual(
      ['Rome', 'Amalfi', 'Naples', 'Reykjavík'],
    );
  });

  it('should number the stops on the whole route', () => {
    render();

    const numbers = [...element().querySelectorAll('.route__node')].map((node) =>
      node.textContent?.trim(),
    );

    expect(numbers).toEqual(['01', '02', '03', '04']);
  });

  it('should read the dates of a stop, or say they are not decided', () => {
    render();

    const facts = [...element().querySelectorAll('.route__dates-text')].map((node) =>
      node.textContent?.trim(),
    );

    expect(facts).toEqual([
      '1 Apr – 4 Apr 2027',
      'Dates not decided',
      'Dates not decided',
      '6 Apr – 9 Apr 2027',
    ]);
  });

  it('should mark a stop that is not on the map', () => {
    render();

    const flags = [...element().querySelectorAll('.atlas-list__flag')].map((node) =>
      node.textContent?.trim(),
    );

    expect(flags).toEqual(['Not on the map', 'Not on the map']);
  });

  it('should own up to a route that is only partly on the map', () => {
    render();

    expect(element().querySelector('.atlas-list__note')?.textContent).toContain(
      '2 of 4 stops are on the map',
    );
  });

  it('should say when there are no stops at all', () => {
    render({ destinations: [] });

    expect(element().textContent).toContain('No destinations recorded');
    expect(stopRows()).toHaveLength(0);
  });

  it('should mark the selected stop', () => {
    render({ selection: { kind: 'destination', id: 'd4' } });

    const pressed = stopRows().filter((row) => row.getAttribute('aria-pressed') === 'true');

    expect(pressed).toHaveLength(1);
    expect(pressed[0].textContent).toContain('Reykjavík');
  });

  it('should say which stop was chosen', () => {
    render();

    const chosen: { kind: string; id: string }[] = [];
    fixture.componentInstance.select.subscribe((selection) => chosen.push(selection));

    stopRows()[2].click();

    expect(chosen).toEqual([{ kind: 'destination', id: 'c3' }]);
  });

  it('should list the saved places with their category and context', () => {
    render({ places });

    expect(placeRows()).toHaveLength(2);

    const first = placeRows()[0];

    expect(first.textContent).toContain('Fushimi Inari');
    expect(first.querySelector('.atlas-places__category')?.textContent).toContain('Sight');
    expect(first.querySelector('.atlas-places__where')?.textContent).toContain('Kyoto');
  });

  it('should say which stop a place is filed under', () => {
    render({ places });

    const first = placeRows()[0];

    expect(first.querySelector('.atlas-places__stop')?.textContent).toContain('Near Rome');
  });

  it('should say nothing about a stop when a place is filed under none', () => {
    render({ places });

    expect(placeRows()[1].querySelector('.atlas-places__stop')).toBeNull();
  });

  it('should mark the selected place, and only the selected one', () => {
    render({ places, selection: { kind: 'place', id: 'p2' } });

    const pressed = placeRows().filter((row) => row.getAttribute('aria-pressed') === 'true');

    expect(pressed).toHaveLength(1);
    expect(pressed[0].textContent).toContain('Nishiki Market');
    expect(stopRows().some((row) => row.getAttribute('aria-pressed') === 'true')).toBe(false);
  });

  it('should say which place was chosen', () => {
    render({ places });

    const chosen: { kind: string; id: string }[] = [];
    fixture.componentInstance.select.subscribe((selection) => chosen.push(selection));

    placeRows()[1].click();

    expect(chosen).toEqual([{ kind: 'place', id: 'p2' }]);
  });

  it('should say how many places are saved', () => {
    render({ places });

    expect(element().querySelector('.atlas-list__places-section .section-head__meta')?.textContent).toContain(
      '2 places',
    );
  });

  it('should say when nothing is saved yet', () => {
    render();

    expect(element().textContent).toContain('Nothing saved yet');
  });

  it('should say when the places are still loading, without hiding the route', () => {
    render({ placesLoading: true });

    expect(element().textContent).toContain('Loading saved places');
    expect(stopRows()).toHaveLength(4);
  });

  it('should say when the places could not be loaded, and offer to try again', () => {
    render({ placesError: 'The Tabiji API could not be reached.' });

    expect(element().textContent).toContain('The Tabiji API could not be reached.');

    // The route is still there: a failure of the places is not a failure of
    // the trip.
    expect(stopRows()).toHaveLength(4);

    let retried = false;
    fixture.componentInstance.retryPlaces.subscribe(() => (retried = true));

    const retry = [...element().querySelectorAll('button')].find((button) =>
      button.textContent?.includes('Try again'),
    ) as HTMLButtonElement;

    retry.click();

    expect(retried).toBe(true);
  });

  it('should treat a place filed under a stop that has gone as unfiled', () => {
    render({
      places: [{ ...places[0], destinationId: 'gone' }],
    });

    expect(placeRows()[0].querySelector('.atlas-places__stop')).toBeNull();
  });
});
