import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TripDestination, TripStop } from '../../core/models/trip';
import { StopDraft, StopEditor } from './stop-editor';

const parent: TripDestination = {
  id: 'kyoto',
  name: 'Kyoto',
  sortOrder: 0,
  startsOn: '2027-04-01',
  endsOn: '2027-04-04',
  latitude: 35,
  longitude: 135,
  stops: [],
};

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

describe('StopEditor', () => {
  let fixture: ComponentFixture<StopEditor>;
  let saved: StopDraft[];
  let dismissed: number;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StopEditor],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(StopEditor);
    saved = [];
    dismissed = 0;

    fixture.componentInstance.save.subscribe((draft) => saved.push(draft));
    fixture.componentInstance.dismissed.subscribe(() => (dismissed += 1));
  });

  function show(options: {
    parent?: TripDestination | null;
    stop?: TripStop | null;
    tripStartsOn?: string;
    tripEndsOn?: string;
  } = {}): void {
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('parent', options.parent ?? parent);
    fixture.componentRef.setInput('stop', options.stop ?? null);
    fixture.componentRef.setInput('tripStartsOn', options.tripStartsOn ?? '2027-04-01');
    fixture.componentRef.setInput('tripEndsOn', options.tripEndsOn ?? '2027-04-10');
    fixture.detectChanges();
  }

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function input(selector: string): HTMLInputElement {
    return element().querySelector(selector) as HTMLInputElement;
  }

  function set(selector: string, value: string, event = 'input'): void {
    const control = input(selector);
    control.value = value;
    control.dispatchEvent(new Event(event));
  }

  function submit(): void {
    const form = element().querySelector('form');
    form?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  it('adds a stop with a name and no coordinates', () => {
    show();
    set('#stop-name', 'Gion');

    submit();

    expect(saved).toEqual([
      {
        name: 'Gion',
        startsOn: null,
        endsOn: null,
        latitude: null,
        longitude: null,
        category: null,
        address: null,
        notes: null,
        plannedOn: null,
      },
    ]);
  });

  it('refuses a blank name and stays open', () => {
    show();

    submit();

    expect(saved).toEqual([]);
    expect(element().textContent).toContain('This field is required.');
  });

  it('refuses a planned day outside the trip', () => {
    show();
    set('#stop-name', 'Gion');
    set('#stop-planned-on', '2027-04-15');

    submit();

    expect(saved).toEqual([]);
    expect(element().textContent).toContain('after the trip ends');
  });

  it('refuses a stay outside the destination', () => {
    show();
    set('#stop-name', 'Gion');
    set('#stop-starts-on', '2027-04-06');

    submit();

    expect(saved).toEqual([]);
    expect(element().textContent).toContain('after the destination ends');
  });

  it('refuses a stay that ends before it starts', () => {
    show();
    set('#stop-name', 'Gion');
    set('#stop-starts-on', '2027-04-03');
    set('#stop-ends-on', '2027-04-02');

    submit();

    expect(saved).toEqual([]);
    expect(element().textContent).toContain('end date must be on or after');
  });

  it('carries the category, notes, planned day and stay dates', () => {
    show();

    const sight = [...element().querySelectorAll<HTMLOptionElement>('#stop-category option')].find(
      (option) => option.value === 'Sight',
    );
    expect(sight?.textContent).toBe('Sightseeing');

    set('#stop-name', 'Gion');
    set('#stop-category', 'Sight', 'change');
    set('#stop-notes', 'Go early.');
    set('#stop-planned-on', '2027-04-02');
    set('#stop-starts-on', '2027-04-02');
    set('#stop-ends-on', '2027-04-03');

    submit();

    expect(saved).toHaveLength(1);
    expect(saved[0]).toMatchObject({
      name: 'Gion',
      category: 'Sight',
      notes: 'Go early.',
      plannedOn: '2027-04-02',
      startsOn: '2027-04-02',
      endsOn: '2027-04-03',
    });
  });

  it('offers the categories alphabetically with Other last', () => {
    show();

    const options = [
      ...element().querySelectorAll<HTMLOptionElement>('#stop-category option'),
    ].slice(1);

    expect(options.map((option) => option.textContent?.trim())).toEqual([
      'Accommodation',
      'Entertainment',
      'Food',
      'Shop',
      'Sightseeing',
      'Transport',
      'Other',
    ]);
    expect(options.map((option) => option.value)).toEqual([
      'Stay',
      'Entertainment',
      'Food',
      'Shop',
      'Sight',
      'Transport',
      'Other',
    ]);
  });

  it('edits the stop it was opened on, keeping its details', () => {
    show({ stop: gion });

    expect(input('#stop-name').value).toBe('Gion');

    set('#stop-name', 'Gion at night');
    submit();

    expect(saved).toHaveLength(1);
    expect(saved[0]).toMatchObject({
      name: 'Gion at night',
      category: 'Sight',
      address: 'Gion, Kyoto',
      notes: 'Go early.',
      plannedOn: '2027-04-02',
      latitude: 35.0037,
      longitude: 135.7788,
    });
  });

  it('backs out without saving', () => {
    show();
    set('#stop-name', 'Gion');

    element()
      .querySelectorAll<HTMLButtonElement>('.editor__actions button')[0]
      .click();

    expect(dismissed).toBe(1);
    expect(saved).toEqual([]);
  });
});
