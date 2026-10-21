import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TripCreateForm } from './trip-create-form';

const validTrip = {
  name: 'Rome & Amalfi',
  destinations: ['Rome', 'Amalfi', 'Naples'],
  startsOn: '2027-04-01',
  endsOn: '2027-04-14',
};

describe('TripCreateForm', () => {
  let fixture: ComponentFixture<TripCreateForm>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [TripCreateForm],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(TripCreateForm);
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

  function type(index: number, value: string): void {
    const field = input(index);

    field.value = value;
    field.dispatchEvent(new Event('input'));
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
    setDestinations(validTrip.destinations);
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
    setDestinations(validTrip.destinations);
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
    setDestinations(validTrip.destinations);
    setTripFields();

    submit();

    const request = http.expectOne('/api/trips');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(validTrip);

    request.flush({
      id: '5f2b1c4e-0000-0000-0000-000000000000',
      name: validTrip.name,
      destinations: validTrip.destinations.map((name, sortOrder) => ({ name, sortOrder })),
      startsOn: validTrip.startsOn,
      endsOn: validTrip.endsOn,
      status: 'Planning',
    });

    fixture.detectChanges();

    expect(element().textContent).toContain('Created "Rome & Amalfi".');
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
    setDestinations(validTrip.destinations);

    action('Move Naples up').click();
    fixture.detectChanges();

    expect(rows().map((row) => row.querySelector('input')?.value)).toEqual([
      'Rome',
      'Naples',
      'Amalfi',
    ]);
  });

  it('should move a stop down', () => {
    setDestinations(validTrip.destinations);

    action('Move Rome down').click();
    fixture.detectChanges();

    expect(rows().map((row) => row.querySelector('input')?.value)).toEqual([
      'Amalfi',
      'Rome',
      'Naples',
    ]);
  });

  it('should not move the first stop up or the last stop down', () => {
    setDestinations(validTrip.destinations);

    expect(action('Move Rome up').disabled).toBe(true);
    expect(action('Move Naples down').disabled).toBe(true);
  });

  it('should send the stops in the order they are shown', () => {
    setDestinations(validTrip.destinations);
    action('Move Naples up').click();
    setTripFields();

    submit();

    const request = http.expectOne('/api/trips');

    expect(request.request.body.destinations).toEqual(['Rome', 'Naples', 'Amalfi']);
  });

  it('should number the stops', () => {
    setDestinations(validTrip.destinations);

    const nodes = element().querySelectorAll('.route__node');

    expect(nodes.length).toBe(3);
    expect(nodes[0].textContent).toContain('01');
    expect(nodes[2].textContent).toContain('03');
  });

  it('should give every stop a labelled input', () => {
    setDestinations(validTrip.destinations);

    const labels = [...element().querySelectorAll('label.route__node')] as HTMLLabelElement[];

    expect(labels.length).toBe(3);

    for (const label of labels) {
      const target = element().querySelector(`#${label.getAttribute('for')}`);

      expect(target).not.toBeNull();
    }
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

  it('should emit the created trip', () => {
    const created: unknown[] = [];
    fixture.componentInstance.created.subscribe((trip) => created.push(trip));

    setDestinations(validTrip.destinations);
    setTripFields();
    submit();

    http.expectOne('/api/trips').flush({
      id: '5f2b1c4e-0000-0000-0000-000000000000',
      name: validTrip.name,
      destinations: validTrip.destinations.map((name, sortOrder) => ({ name, sortOrder })),
      startsOn: validTrip.startsOn,
      endsOn: validTrip.endsOn,
      status: 'Planning',
    });

    expect(created).toHaveLength(1);
  });

  it('should show the message from a rejected trip', () => {
    setDestinations(validTrip.destinations);
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
});
