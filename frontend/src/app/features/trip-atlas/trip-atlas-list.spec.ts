import { ComponentFixture, TestBed } from '@angular/core/testing';
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

  function render(destinations: TripDestination[], selectedId: string | null = null): void {
    fixture.componentRef.setInput('destinations', destinations);
    fixture.componentRef.setInput('selectedId', selectedId);
    fixture.detectChanges();
  }

  function rows(): HTMLButtonElement[] {
    return [...element().querySelectorAll('.atlas-list__item')] as HTMLButtonElement[];
  }

  it('should list every stop, in order', () => {
    render(route);

    expect(rows().map((row) => row.querySelector('.atlas-list__name')?.textContent?.trim())).toEqual(
      ['Rome', 'Amalfi', 'Naples', 'Reykjavík'],
    );
  });

  it('should number the stops on the whole route', () => {
    render(route);

    const numbers = [...element().querySelectorAll('.route__node')].map((node) =>
      node.textContent?.trim(),
    );

    expect(numbers).toEqual(['01', '02', '03', '04']);
  });

  it('should read the dates of a stop, or say they are not decided', () => {
    render(route);

    const facts = [...element().querySelectorAll('.route__dates-text')].map((node) =>
      node.textContent?.trim(),
    );

    expect(facts).toEqual(['1 Apr – 4 Apr 2027', 'Dates not decided', 'Dates not decided', '6 Apr – 9 Apr 2027']);
  });

  it('should mark a stop that is not on the map', () => {
    render(route);

    const flags = [...element().querySelectorAll('.atlas-list__flag')].map((node) =>
      node.textContent?.trim(),
    );

    expect(flags).toEqual(['Not on the map', 'Not on the map']);
  });

  it('should own up to a route that is only partly on the map', () => {
    render(route);

    expect(element().querySelector('.atlas-list__note')?.textContent).toContain(
      '2 of 4 stops are on the map',
    );
  });

  it('should say nothing about the map when every stop is on it', () => {
    render([route[0], route[3]]);

    expect(element().querySelector('.atlas-list__note')).toBeNull();
  });

  it('should say so when no stop is on the map yet', () => {
    render([route[1], route[2]]);

    expect(element().querySelector('.atlas-list__note')?.textContent).toContain(
      'No stop is on the map yet',
    );
  });

  it('should say when there are no stops at all', () => {
    render([]);

    expect(element().textContent).toContain('No destinations recorded');
    expect(rows()).toHaveLength(0);
  });

  it('should mark the selected stop', () => {
    render(route, 'd4');

    const pressed = rows().filter((row) => row.getAttribute('aria-pressed') === 'true');

    expect(pressed).toHaveLength(1);
    expect(pressed[0].textContent).toContain('Reykjavík');
  });

  it('should say which stop was chosen', () => {
    render(route);

    const chosen: string[] = [];
    fixture.componentInstance.select.subscribe((id) => chosen.push(id));

    rows()[2].click();

    expect(chosen).toEqual(['c3']);
  });
});
