import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SavedPlace } from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';
import { TripAtlasPanel } from './trip-atlas-panel';

const kyoto: TripDestination = {
  id: 'a1',
  name: 'Kyoto',
  sortOrder: 0,
  startsOn: '2027-04-01',
  endsOn: '2027-04-04',
  latitude: 35.0116,
  longitude: 135.7681,
};

const nara: TripDestination = {
  id: 'b2',
  name: 'Nara',
  sortOrder: 1,
  startsOn: null,
  endsOn: null,
  latitude: null,
  longitude: null,
};

const destinations: TripDestination[] = [kyoto, nara];

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
  destinationId: 'a1',
  name: 'Nishiki Market',
  latitude: 35.005,
  longitude: 135.7649,
  category: 'Food',
  address: null,
  notes: null,
  createdAt: '2026-10-20T10:00:00+00:00',
};

const unfiled: SavedPlace = {
  id: 'p3',
  destinationId: null,
  name: 'Blue Lagoon',
  latitude: 63.8804,
  longitude: -22.4495,
  category: 'Stay',
  address: null,
  notes: null,
  createdAt: '2026-10-20T11:00:00+00:00',
};

const places: SavedPlace[] = [fushimi, nishiki, unfiled];

describe('TripAtlasPanel', () => {
  let fixture: ComponentFixture<TripAtlasPanel>;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [TripAtlasPanel],
    }).compileComponents();

    fixture = TestBed.createComponent(TripAtlasPanel);
    fixture.componentRef.setInput('destinations', destinations);
    fixture.componentRef.setInput('places', places);
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function render(options: {
    destination?: TripDestination | null;
    place?: SavedPlace | null;
    position?: number | null;
    total?: number;
  }): void {
    fixture.componentRef.setInput('destination', options.destination ?? null);
    fixture.componentRef.setInput('place', options.place ?? null);
    fixture.componentRef.setInput('position', options.position ?? null);
    fixture.componentRef.setInput('total', options.total ?? 0);
    fixture.detectChanges();
  }

  function placeButtons(): HTMLButtonElement[] {
    return [...element().querySelectorAll('.atlas-panel__place')] as HTMLButtonElement[];
  }

  function button(text: string): HTMLButtonElement {
    const found = [...element().querySelectorAll('button')].find((candidate) =>
      candidate.textContent?.includes(text),
    );

    return found as HTMLButtonElement;
  }

  it('should ask for a selection when nothing is selected', () => {
    render({});

    expect(element().textContent).toContain('Select a stop or a saved place');
  });

  describe('a selected stop', () => {
    it('should list the places filed under it, with a count', () => {
      render({ destination: kyoto, position: 1, total: 2 });

      expect(placeButtons()).toHaveLength(2);
      expect(element().querySelector('.atlas-panel__count')?.textContent).toContain('2 places');
      expect(element().textContent).toContain('Fushimi Inari');
      expect(element().textContent).toContain('Nishiki Market');
    });

    it('should leave out another stop’s places and the unfiled ones', () => {
      render({ destination: nara, position: 2, total: 2 });

      // Nara has nothing filed under it, so Blue Lagoon (unfiled) must not
      // appear here either.
      expect(placeButtons()).toHaveLength(0);
    });

    it('should say so when nothing is filed under it', () => {
      render({ destination: nara, position: 2, total: 2 });

      expect(element().textContent).toContain('Nothing saved here yet');
    });

    it('should ask for a place to be selected when one is clicked', () => {
      render({ destination: kyoto, position: 1, total: 2 });

      const chosen: SavedPlace[] = [];
      fixture.componentInstance.selectPlace.subscribe((place) => chosen.push(place));

      placeButtons()[1].click();

      expect(chosen.map((place) => place.id)).toEqual(['p2']);
    });

    it('should move the focus to the place it selected', async () => {
      render({ destination: kyoto, position: 1, total: 2 });

      placeButtons()[1].click();

      // The screen answers by showing that place in place of the stop, which
      // takes the button the reader pressed with it.
      fixture.componentRef.setInput('destination', null);
      fixture.componentRef.setInput('place', nishiki);
      fixture.detectChanges();
      await fixture.whenStable();

      // The focus follows the content, so a keyboard reader is not dropped
      // back at the top of the page.
      expect(document.activeElement?.textContent).toContain('Nishiki Market');
    });

    it('should still offer the stop’s own actions', () => {
      render({ destination: kyoto, position: 1, total: 2 });

      const changed: TripDestination[] = [];
      fixture.componentInstance.changeLocation.subscribe((stop) => changed.push(stop));

      button('Change location').click();

      expect(changed).toEqual([kyoto]);
    });

    it('should keep the stop’s dates and location readable', () => {
      render({ destination: kyoto, position: 1, total: 2 });

      expect(element().textContent).toContain('1 Apr – 4 Apr 2027');
      expect(element().textContent).toContain('On the map');
      expect(element().textContent).toContain('Stop 1 of 2');
    });
  });

  describe('a selected place', () => {
    it('should show everything it carries', () => {
      render({ place: fushimi });

      expect(element().querySelector('.atlas-panel__name')?.textContent).toContain('Fushimi Inari');
      expect(element().textContent).toContain('68 Fukakusa Yabunouchicho, Kyoto');
      expect(element().textContent).toContain('Go early.');
      expect(element().textContent).toContain('Kyoto');
      expect(element().textContent).toContain('34.9671');
    });

    it('should say when it is filed under no stop', () => {
      render({ place: unfiled });

      expect(element().textContent).toContain('Not on the route');
    });

    it('should say when its stop has gone', () => {
      render({ place: { ...fushimi, destinationId: 'gone' } });

      expect(element().textContent).toContain('Not on the route');
    });

    it('should offer editing and removal', () => {
      render({ place: fushimi });

      const edited: SavedPlace[] = [];
      const removed: SavedPlace[] = [];
      fixture.componentInstance.editPlace.subscribe((place) => edited.push(place));
      fixture.componentInstance.deletePlace.subscribe((place) => removed.push(place));

      button('Edit place').click();
      button('Delete place').click();

      expect(edited.map((place) => place.id)).toEqual(['p1']);
      expect(removed.map((place) => place.id)).toEqual(['p1']);
    });

    it('should not list the stop’s places', () => {
      render({ place: fushimi });

      expect(placeButtons()).toHaveLength(0);
    });
  });
});
