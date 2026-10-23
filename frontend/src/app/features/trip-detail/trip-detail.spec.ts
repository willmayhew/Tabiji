import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { Trip } from '../../core/models/trip';
import { TripDetail } from './trip-detail';
import { unsavedChangesGuard } from './trip-detail.guard';

const trip: Trip = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Rome & Amalfi',
  destinations: [
    {
      id: 'a1',
      name: 'Rome',
      sortOrder: 0,
      startsOn: '2027-04-01',
      endsOn: '2027-04-04',
      latitude: null,
      longitude: null,
    },
    {
      id: 'b2',
      name: 'Amalfi',
      sortOrder: 1,
      startsOn: '2027-04-04',
      endsOn: '2027-04-06',
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
  ],
  startsOn: '2027-04-01',
  endsOn: '2027-04-08',
  status: 'Planning',
};

describe('TripDetail', () => {
  let fixture: ComponentFixture<TripDetail>;
  let http: HttpTestingController;
  let paramMap: BehaviorSubject<ParamMap>;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    paramMap = new BehaviorSubject<ParamMap>(convertToParamMap({ id: '' }));

    await TestBed.configureTestingModule({
      imports: [TripDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap } },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function createComponent(id: string): void {
    paramMap.next(convertToParamMap({ id }));
    fixture = TestBed.createComponent(TripDetail);
    fixture.detectChanges();
  }

  /** Loads the trip and leaves the page showing it. */
  function showTrip(): void {
    createComponent(trip.id);

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();
  }

  /** Loads the trip and puts the page into edit mode. */
  function startEditing(): void {
    showTrip();

    const edit = element().querySelector('.detail__edit-button') as HTMLButtonElement;

    edit.click();
    fixture.detectChanges();
  }

  it('should show a loading state while the trip is fetched', () => {
    createComponent(trip.id);

    expect(element().textContent).toContain('Loading trip');

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();
  });

  it('should show the trip', () => {
    showTrip();

    expect(element().textContent).toContain('Rome & Amalfi');
    expect(element().textContent).toContain('Rome');
    expect(element().textContent).toContain('1 Apr – 8 Apr 2027');
  });

  it('should set the route out in order', () => {
    showTrip();

    const names = [...element().querySelectorAll('.route__name')].map(
      (node) => node.textContent?.trim(),
    );

    expect(names).toEqual(['Rome', 'Amalfi', 'Naples']);
  });

  it('should show the dates of each stop', () => {
    showTrip();

    const dates = [...element().querySelectorAll('.route__dates-text')].map(
      (node) => node.textContent?.trim(),
    );

    expect(dates).toEqual(['1 Apr – 4 Apr 2027', '4 Apr – 6 Apr 2027', 'Dates not decided']);
  });

  it('should say how much of the schedule has been decided', () => {
    showTrip();

    expect(element().textContent).toContain('Dates are set for 2 of 3 stops');
  });

  it('should say so when a trip has no stops', () => {
    createComponent(trip.id);

    http.expectOne(`/api/trips/${trip.id}`).flush({ ...trip, destinations: [] });
    fixture.detectChanges();

    expect(element().textContent).toContain('No destinations recorded');
  });

  it('should keep the machine readable dates on the time elements', () => {
    showTrip();

    const hero = element().querySelectorAll('.detail__meta time');
    const details = element().querySelectorAll('.detail__list time');

    expect(hero.length).toBe(2);
    expect(hero[0].getAttribute('datetime')).toBe('2027-04-01');
    expect(hero[1].getAttribute('datetime')).toBe('2027-04-08');

    expect(details.length).toBe(2);
    expect(details[0].getAttribute('datetime')).toBe('2027-04-01');
    expect(details[1].getAttribute('datetime')).toBe('2027-04-08');
  });

  it('should show a not found state for an unknown trip', () => {
    createComponent('99999999-9999-9999-9999-999999999999');

    http
      .expectOne('/api/trips/99999999-9999-9999-9999-999999999999')
      .flush({ title: 'Trip not found', status: 404 }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(element().textContent).toContain('That trip could not be found');
  });

  it('should show an error state when the API fails', () => {
    createComponent(trip.id);

    http
      .expectOne(`/api/trips/${trip.id}`)
      .flush({ title: 'Server error', status: 500 }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(element().textContent).toContain('Server error');
  });

  it('should try the request again after an error', () => {
    createComponent(trip.id);

    http
      .expectOne(`/api/trips/${trip.id}`)
      .flush({ title: 'Server error', status: 500 }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(element().textContent).toContain('Server error');

    const retry = element().querySelector('.empty-state button') as HTMLButtonElement;
    retry.click();
    fixture.detectChanges();

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();

    expect(element().textContent).toContain('Rome & Amalfi');
    expect(element().textContent).not.toContain('Server error');
  });

  it('should link back to the trips list', () => {
    showTrip();

    const back = element().querySelector('.back-link') as HTMLAnchorElement;

    expect(back.getAttribute('href')).toBe('/trips');
  });

  it('should render the header without an image or a plate', () => {
    showTrip();

    expect(element().querySelector('img')).toBeNull();
    expect(element().querySelector('.plate')).toBeNull();
  });

  it('should show the trip name as the only heading', () => {
    showTrip();

    const headings = element().querySelectorAll('h1, h2, h3');

    expect(element().querySelectorAll('h1')).toHaveLength(1);
    expect(headings[0].tagName).toBe('H1');
    expect(headings[0].textContent).toContain('Rome & Amalfi');
  });

  it('should let the reader change the trip', () => {
    startEditing();

    expect(element().querySelector('#trip-name')).not.toBeNull();
    expect((element().querySelector('#trip-destination-0') as HTMLInputElement).value).toBe('Rome');
    expect((element().querySelector('#trip-destination-0-startsOn') as HTMLInputElement).value).toBe(
      '2027-04-01',
    );
  });

  it('should put the whole trip back when the reader saves it', () => {
    startEditing();

    const name = element().querySelector('#trip-name') as HTMLInputElement;
    name.value = 'Italia';
    name.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const form = element().querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const request = http.expectOne(`/api/trips/${trip.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Italia',
      startsOn: '2027-04-01',
      endsOn: '2027-04-08',
      destinations: [
        { id: 'a1', name: 'Rome', startsOn: '2027-04-01', endsOn: '2027-04-04', latitude: null, longitude: null },
        { id: 'b2', name: 'Amalfi', startsOn: '2027-04-04', endsOn: '2027-04-06', latitude: null, longitude: null },
        { id: 'c3', name: 'Naples', startsOn: null, endsOn: null, latitude: null, longitude: null },
      ],
    });

    request.flush({ ...trip, name: 'Italia' });
    fixture.detectChanges();

    expect(element().textContent).toContain('Italia');
    expect(element().querySelector('.detail__edit-button')).not.toBeNull();
  });

  it('should back out of a change without saving it', () => {
    startEditing();

    const name = element().querySelector('#trip-name') as HTMLInputElement;
    name.value = 'Italia';
    name.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const cancel = [...element().querySelectorAll('button')].find(
      (button) => button.textContent?.trim() === 'Cancel',
    ) as HTMLButtonElement;

    cancel.click();
    fixture.detectChanges();

    http.expectNone(`/api/trips/${trip.id}`);
    expect(element().querySelector('#trip-name')).toBeNull();
    expect(element().textContent).toContain('Rome & Amalfi');
  });

  it('should show the message from a rejected change', () => {
    startEditing();

    const form = element().querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    http
      .expectOne(`/api/trips/${trip.id}`)
      .flush(
        { title: 'Invalid trip', status: 400, detail: 'The trip name is required.' },
        { status: 400, statusText: 'Bad Request' },
      );
    fixture.detectChanges();

    expect(element().textContent).toContain('The trip name is required.');
  });
});

describe('unsavedChangesGuard', () => {
  it('should let the reader go when nothing has been changed', () => {
    const confirm = vi.spyOn(window, 'confirm');
    const detail = { hasUnsavedChanges: () => false } as TripDetail;

    expect(unsavedChangesGuard(detail)).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
  });

  it('should ask before leaving a trip that has been changed', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const detail = { hasUnsavedChanges: () => true } as TripDetail;

    expect(unsavedChangesGuard(detail)).toBe(false);
    expect(confirm).toHaveBeenCalledWith('You have unsaved changes. Leave without saving?');
  });
});
