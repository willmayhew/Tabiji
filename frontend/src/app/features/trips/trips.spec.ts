import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Trip } from '../../core/models/trip';
import { Trips } from './trips';

const tokyo: Trip = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Tokyo',
  destination: 'Kanto',
  startsOn: '2027-04-01',
  endsOn: '2027-04-08',
  status: 'Planning',
};

const kyoto: Trip = {
  id: '22222222-2222-2222-2222-222222222222',
  name: 'Kyoto',
  destination: 'Kansai',
  startsOn: '2027-04-10',
  endsOn: '2027-04-14',
  status: 'Confirmed',
};

describe('Trips', () => {
  let fixture: ComponentFixture<Trips>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [Trips],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(Trips);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('should show a loading state while the trips are fetched', () => {
    fixture.detectChanges();

    expect(element().textContent).toContain('Loading trips');

    http.expectOne('/api/trips').flush([tokyo, kyoto]);
    fixture.detectChanges();
  });

  it('should list the trips returned by the API', () => {
    fixture.detectChanges();

    http.expectOne('/api/trips').flush([tokyo, kyoto]);
    fixture.detectChanges();

    expect(element().textContent).toContain('Tokyo');
    expect(element().textContent).toContain('Kyoto');
    expect(element().querySelectorAll('.trip-link')).toHaveLength(2);
  });

  it('should link every trip to its detail page', () => {
    fixture.detectChanges();

    http.expectOne('/api/trips').flush([tokyo]);
    fixture.detectChanges();

    const link = element().querySelector('.trip-link') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/trips/11111111-1111-1111-1111-111111111111');
  });

  it('should show an empty state when there are no trips', () => {
    fixture.detectChanges();

    http.expectOne('/api/trips').flush([]);
    fixture.detectChanges();

    expect(element().textContent).toContain('No trips yet');
    expect(element().querySelectorAll('.trip-link')).toHaveLength(0);
  });

  it('should show an error state when the trips cannot be loaded', () => {
    fixture.detectChanges();

    http.expectOne('/api/trips').flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(element().textContent).toContain('The API returned 500.');
    expect(element().textContent).toContain('Try again');
  });

  it('should reload the trips when the refresh button is clicked', () => {
    fixture.detectChanges();

    http.expectOne('/api/trips').flush([tokyo]);
    fixture.detectChanges();

    const refresh = element().querySelector('.button--secondary') as HTMLButtonElement;
    refresh.click();
    fixture.detectChanges();

    expect(element().textContent).toContain('Loading trips');

    http.expectOne('/api/trips').flush([tokyo, kyoto]);
    fixture.detectChanges();

    expect(element().querySelectorAll('.trip-link')).toHaveLength(2);
  });

  it('should promote the first trip to a feature band', () => {
    fixture.detectChanges();

    http.expectOne('/api/trips').flush([tokyo, kyoto]);
    fixture.detectChanges();

    const items = element().querySelectorAll('.trip-grid > li');

    expect(items.length).toBe(2);
    expect(items[0].classList.contains('trip-grid__item--feature')).toBe(true);
    expect(items[1].classList.contains('trip-grid__item--feature')).toBe(false);
  });

  it('should number the trips from one', () => {
    fixture.detectChanges();

    http.expectOne('/api/trips').flush([tokyo, kyoto]);
    fixture.detectChanges();

    const indexes = element().querySelectorAll('.plate__index');

    expect(indexes.length).toBe(2);
    expect(indexes[0].textContent).toContain('No. 01');
    expect(indexes[1].textContent).toContain('No. 02');
  });

  it('should not render an image in the grid', () => {
    fixture.detectChanges();

    http.expectOne('/api/trips').flush([tokyo, kyoto]);
    fixture.detectChanges();

    expect(element().querySelector('img')).toBeNull();
  });
});
