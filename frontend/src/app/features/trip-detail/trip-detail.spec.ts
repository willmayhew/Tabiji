import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { Trip } from '../../core/models/trip';
import { TripDetail } from './trip-detail';

const trip: Trip = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Tokyo',
  destination: 'Kanto',
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

  it('should show a loading state while the trip is fetched', () => {
    createComponent(trip.id);

    expect(element().textContent).toContain('Loading trip');

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();
  });

  it('should show the trip', () => {
    createComponent(trip.id);

    const request = http.expectOne(`/api/trips/${trip.id}`);
    expect(request.request.method).toBe('GET');
    request.flush(trip);
    fixture.detectChanges();

    expect(element().textContent).toContain('Tokyo');
    expect(element().textContent).toContain('Kanto');
    expect(element().textContent).toContain('1 Apr – 8 Apr 2027');
  });

  it('should keep the machine readable dates on the time elements', () => {
    createComponent(trip.id);

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();

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

    expect(element().textContent).toContain('Tokyo');
    expect(element().textContent).not.toContain('Server error');
  });

  it('should link back to the trips list', () => {
    createComponent(trip.id);

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();

    const back = element().querySelector('.back-link') as HTMLAnchorElement;

    expect(back.getAttribute('href')).toBe('/trips');
  });

  it('should render the header without an image', () => {
    createComponent(trip.id);

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();

    expect(element().querySelector('img')).toBeNull();
    expect(element().querySelector('.detail__hero .plate')).not.toBeNull();
  });

  it('should show the trip name as the only heading', () => {
    createComponent(trip.id);

    http.expectOne(`/api/trips/${trip.id}`).flush(trip);
    fixture.detectChanges();

    const headings = element().querySelectorAll('h1, h2, h3');

    expect(element().querySelectorAll('h1')).toHaveLength(1);
    expect(headings[0].tagName).toBe('H1');
    expect(headings[0].textContent).toContain('Tokyo');
  });
});
