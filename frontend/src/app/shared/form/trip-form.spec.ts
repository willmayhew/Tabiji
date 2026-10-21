import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Trip } from '../../core/models/trip';
import { TripForm } from './trip-form';

const validTrip = {
  name: 'Rome & Amalfi',
  destinations: [
    { name: 'Rome', startsOn: '2027-04-01', endsOn: '2027-04-05' },
    { name: 'Amalfi', startsOn: '2027-04-05', endsOn: '2027-04-08' },
    { name: 'Naples', startsOn: null, endsOn: null },
  ],
  startsOn: '2027-04-01',
  endsOn: '2027-04-14',
};

const existingTrip: Trip = {
  id: '5f2b1c4e-0000-0000-0000-000000000000',
  name: 'Rome & Amalfi',
  destinations: [
    {
      id: 'a1000000-0000-0000-0000-000000000000',
      name: 'Rome',
      sortOrder: 0,
      startsOn: '2027-04-01',
      endsOn: '2027-04-05',
    },
    {
      id: 'b2000000-0000-0000-0000-000000000000',
      name: 'Amalfi',
      sortOrder: 1,
      startsOn: null,
      endsOn: null,
    },
  ],
  startsOn: '2027-04-01',
  endsOn: '2027-04-14',
  status: 'Planning',
};

describe('TripForm', () => {
  let fixture: ComponentFixture<TripForm>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [TripForm],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(TripForm);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function submit(): void {
    const form = element().querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  function rows(): HTMLElement[] {
    return [...element().querySelectorAll('.route__row')] as HTMLElement[];
  }

  function input(index: number): HTMLInputElement {
    return element().querySelector(`#trip-destination-${index}`) as HTMLInputElement;
  }

  function dateInput(index: number, field: 'startsOn' | 'endsOn'): HTMLInputElement {
    return element().querySelector(`#trip-destination-${index}-${field}`) as HTMLInputElement;
  }

  function type(index: number, value: string): void {
    const field = input(index);

    field.value = value;
    field.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function typeDate(index: number, field: 'startsOn' | 'endsOn', value: string): void {
    const date = dateInput(index, field);

    date.value = value;
    date.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function addStop(): void {
    const button = element().querySelector('.route__add button') as HTMLButtonElement;

    button.click();
    fixture.detectChanges();
  }

  function action(label: string): HTMLButtonElement {
    return element().querySelector(`button[aria-label="${label}"]`) as HTMLButtonElement;
  }

  /** Fills the route, in order, through the interface a reader would use. */
  function setDestinations(names: string[]): void {
    while (rows().length > names.length) {
      const last = rows()[rows().length - 1];
      const remove = last.querySelector('.icon-button--danger') as HTMLButtonElement;

      remove.click();
      fixture.detectChanges();
    }

    while (rows().length < names.length) {
      addStop();
    }

    names.forEach((name, index) => type(index, name));
  }

  function setTripFields(): void {
    fixture.componentInstance.form.patchValue({
      name: validTrip.name,
      startsOn: validTrip.startsOn,
      endsOn: validTrip.endsOn,
    });
  }

  it('should not call the API when required fields are missing', () => {
    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('This field is required.');
  });

  it('should not call the API when the end date is before the start date', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));
    fixture.componentInstance.form.patchValue({
      name: validTrip.name,
      startsOn: '2027-04-14',
      endsOn: '2027-04-01',
    });

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('The end date must be on or after the start date.');
  });

  it('should report a name that is too long', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));
    fixture.componentInstance.form.patchValue({
      name: 'x'.repeat(101),
      startsOn: validTrip.startsOn,
      endsOn: validTrip.endsOn,
    });

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('Use at most 100 characters.');
  });

  it('should not call the API when a stop is blank', () => {
    setDestinations(['Rome', '   ', 'Naples']);
    setTripFields();

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('This field is required.');
  });

  it('should not call the API when the route is empty', () => {
    fixture.componentInstance.form.controls.destinations.clear();
    setTripFields();

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('A trip must have at least one destination.');
  });

  it('should create the trip and report success', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));
    setTripFields();

    submit();

    const request = http.expectOne('/api/trips');
    expect(request.request.method).toBe('POST');

    request.flush({
      id: '5f2b1c4e-0000-0000-0000-000000000000',
      name: validTrip.name,
      destinations: [],
      startsOn: validTrip.startsOn,
      endsOn: validTrip.endsOn,
      status: 'Planning',
    });

    fixture.detectChanges();

    expect(element().textContent).toContain('Created "Rome & Amalfi".');
  });

  it('should send the stops in the order they are shown, with their dates', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));
    setTripFields();
    typeDate(0, 'startsOn', '2027-04-01');
    typeDate(0, 'endsOn', '2027-04-05');
    typeDate(1, 'startsOn', '2027-04-05');
    typeDate(1, 'endsOn', '2027-04-08');

    submit();

    const request = http.expectOne('/api/trips');

    expect(request.request.body.destinations).toEqual([
      { name: 'Rome', startsOn: '2027-04-01', endsOn: '2027-04-05' },
      { name: 'Amalfi', startsOn: '2027-04-05', endsOn: '2027-04-08' },
      { name: 'Naples', startsOn: null, endsOn: null },
    ]);
  });

  it('should send no dates for a stop that has none', () => {
    setDestinations(['Rome']);
    setTripFields();

    submit();

    const request = http.expectOne('/api/trips');

    expect(request.request.body.destinations).toEqual([
      { name: 'Rome', startsOn: null, endsOn: null },
    ]);
  });

  it('should send a cleared date as no date at all', () => {
    setDestinations(['Rome']);
    setTripFields();
    typeDate(0, 'startsOn', '2027-04-01');
    typeDate(0, 'endsOn', '2027-04-05');
    typeDate(0, 'endsOn', '');

    submit();

    const request = http.expectOne('/api/trips');

    expect(request.request.body.destinations).toEqual([
      { name: 'Rome', startsOn: '2027-04-01', endsOn: null },
    ]);
  });

  it('should not call the API when the dates contradict the order', () => {
    setDestinations(['Rome', 'Kyoto']);
    setTripFields();
    typeDate(0, 'startsOn', '2027-04-01');
    typeDate(0, 'endsOn', '2027-04-05');
    typeDate(1, 'startsOn', '2027-04-03');
    typeDate(1, 'endsOn', '2027-04-08');

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('which comes before it');
  });

  it('should not call the API when a date falls outside the trip', () => {
    setDestinations(['Rome']);
    setTripFields();
    typeDate(0, 'startsOn', '2027-05-01');

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('after the trip ends');
  });

  it('should not call the API when a stop ends before it starts', () => {
    setDestinations(['Rome']);
    setTripFields();
    typeDate(0, 'startsOn', '2027-04-05');
    typeDate(0, 'endsOn', '2027-04-01');

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('The end date must be on or after the start date.');
  });

  it('should show the message against the stop that is at fault', () => {
    setDestinations(['Rome', 'Kyoto']);
    setTripFields();
    typeDate(0, 'startsOn', '2027-04-01');
    typeDate(0, 'endsOn', '2027-04-05');
    typeDate(1, 'startsOn', '2027-04-03');

    fixture.detectChanges();

    const message = element().querySelector('#trip-destination-schedule-1');

    expect(message).not.toBeNull();
    expect(message?.textContent).toContain('Kyoto');
  });

  it('should add a stop', () => {
    addStop();

    expect(rows().length).toBe(2);
  });

  it('should remove a stop', () => {
    setDestinations(['Rome', 'Amalfi']);

    action('Remove Amalfi').click();
    fixture.detectChanges();

    expect(rows().length).toBe(1);
    expect(input(0).value).toBe('Rome');
  });

  it('should not allow the last stop to be removed', () => {
    setDestinations(['Rome']);

    const remove = action('Remove Rome');

    expect(remove.disabled).toBe(true);
  });

  it('should move a stop up', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));

    action('Move Naples up').click();
    fixture.detectChanges();

    expect(rows().map((row) => row.querySelector('input')?.value)).toEqual([
      'Rome',
      'Naples',
      'Amalfi',
    ]);
  });

  it('should move a stop down', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));

    action('Move Rome down').click();
    fixture.detectChanges();

    expect(rows().map((row) => row.querySelector('input')?.value)).toEqual([
      'Amalfi',
      'Rome',
      'Naples',
    ]);
  });

  it('should not move the first stop up or the last stop down', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));

    expect(action('Move Rome up').disabled).toBe(true);
    expect(action('Move Naples down').disabled).toBe(true);
  });

  it('should number the stops', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));

    const nodes = element().querySelectorAll('.route__node');

    expect(nodes.length).toBe(3);
    expect(nodes[0].textContent).toContain('01');
    expect(nodes[2].textContent).toContain('03');
  });

  it('should give every stop a labelled input', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));

    const labels = [...element().querySelectorAll('label.route__node')] as HTMLLabelElement[];

    expect(labels.length).toBe(3);

    for (const label of labels) {
      const target = element().querySelector(`#${label.getAttribute('for')}`);

      expect(target).not.toBeNull();
    }
  });

  it('should name the date fields after the stop they belong to', () => {
    setDestinations(['Rome', 'Amalfi']);

    expect(dateInput(0, 'startsOn').getAttribute('aria-label')).toBe('Arrival date at Rome');
    expect(dateInput(1, 'endsOn').getAttribute('aria-label')).toBe('Departure date from Amalfi');
  });

  it('should name the buttons after the stop they belong to', () => {
    setDestinations(['Rome', 'Amalfi']);

    expect(action('Move Rome up').getAttribute('aria-label')).toBe('Move Rome up');
    expect(action('Move Rome down').getAttribute('aria-label')).toBe('Move Rome down');
    expect(action('Remove Rome').getAttribute('aria-label')).toBe('Remove Rome');
  });

  it('should tell the reader what a move did', () => {
    setDestinations(['Rome', 'Amalfi']);

    action('Move Amalfi up').click();
    fixture.detectChanges();

    expect(element().querySelector('[aria-live="polite"]')?.textContent).toContain(
      'Moved Amalfi to position 1.',
    );
  });

  it('should keep the focus on the button that moved the stop', () => {
    vi.useFakeTimers();

    setDestinations(['Rome', 'Amalfi', 'Naples']);

    const button = action('Move Naples up');
    button.focus();
    button.click();
    fixture.detectChanges();

    vi.runAllTimers();
    vi.useRealTimers();

    expect(document.activeElement?.getAttribute('aria-label')).toBe('Move Naples up');
  });

  it('should emit the saved trip', () => {
    const saved: unknown[] = [];
    fixture.componentInstance.saved.subscribe((trip) => saved.push(trip));

    setDestinations(validTrip.destinations.map((stop) => stop.name));
    setTripFields();
    submit();

    http.expectOne('/api/trips').flush({
      id: '5f2b1c4e-0000-0000-0000-000000000000',
      name: validTrip.name,
      destinations: [],
      startsOn: validTrip.startsOn,
      endsOn: validTrip.endsOn,
      status: 'Planning',
    });

    expect(saved).toHaveLength(1);
  });

  it('should show the message from a rejected trip', () => {
    setDestinations(validTrip.destinations.map((stop) => stop.name));
    setTripFields();
    submit();

    http.expectOne('/api/trips').flush(
      {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
        title: 'Invalid trip',
        status: 400,
        detail: 'The trip name is required. (Parameter name)',
      },
      { status: 400, statusText: 'Bad Request' },
    );

    fixture.detectChanges();

    expect(element().textContent).toContain('The trip name is required. (Parameter name)');
  });

  it('should lay a trip out in the form', () => {
    fixture.componentRef.setInput('trip', existingTrip);
    fixture.detectChanges();

    expect(rows().length).toBe(2);
    expect(input(0).value).toBe('Rome');
    expect(input(1).value).toBe('Amalfi');
    expect(dateInput(0, 'startsOn').value).toBe('2027-04-01');
    expect(dateInput(0, 'endsOn').value).toBe('2027-04-05');
    expect(dateInput(1, 'startsOn').value).toBe('');
    expect(fixture.componentInstance.form.controls.name.value).toBe('Rome & Amalfi');
    expect(fixture.componentInstance.form.controls.startsOn.value).toBe('2027-04-01');
  });

  it('should offer to save rather than to create when it is editing', () => {
    fixture.componentRef.setInput('trip', existingTrip);
    fixture.detectChanges();

    expect(element().textContent).toContain('Save changes');
    expect(element().textContent).toContain('Cancel');
  });

  it('should put the whole trip back when it is being edited', () => {
    fixture.componentRef.setInput('trip', existingTrip);
    fixture.detectChanges();

    type(0, 'Roma');
    addStop();
    type(2, 'Naples');

    submit();

    const request = http.expectOne(`/api/trips/${existingTrip.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Rome & Amalfi',
      startsOn: '2027-04-01',
      endsOn: '2027-04-14',
      destinations: [
        {
          id: 'a1000000-0000-0000-0000-000000000000',
          name: 'Roma',
          startsOn: '2027-04-01',
          endsOn: '2027-04-05',
        },
        {
          id: 'b2000000-0000-0000-0000-000000000000',
          name: 'Amalfi',
          startsOn: null,
          endsOn: null,
        },
        { id: null, name: 'Naples', startsOn: null, endsOn: null },
      ],
    });
  });

  it('should tell the reader when the trip has been changed', () => {
    fixture.componentRef.setInput('trip', existingTrip);
    fixture.detectChanges();

    expect(fixture.componentInstance.isDirty()).toBe(false);

    type(0, 'Roma');

    expect(fixture.componentInstance.isDirty()).toBe(true);
  });

  it('should tell the reader when they back out of a change', () => {
    fixture.componentRef.setInput('trip', existingTrip);
    fixture.detectChanges();

    const cancelled: unknown[] = [];
    fixture.componentInstance.cancelled.subscribe(() => cancelled.push(true));

    const cancel = [...element().querySelectorAll('button')].find(
      (button) => button.textContent?.trim() === 'Cancel',
    ) as HTMLButtonElement;

    cancel.click();
    fixture.detectChanges();

    expect(cancelled).toHaveLength(1);
  });
});
