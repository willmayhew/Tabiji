import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Trip } from '../../core/models/trip';
import { Home } from './home';

const journeys: Trip[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Rome & Amalfi',
    destinations: [
      { name: 'Rome', sortOrder: 0 },
      { name: 'Amalfi', sortOrder: 1 },
    ],
    startsOn: '2027-05-01',
    endsOn: '2027-05-12',
    status: 'Planning',
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    name: 'Reykjavík & the Ring Road',
    destinations: [{ name: 'Reykjavík', sortOrder: 0 }],
    startsOn: '2027-06-02',
    endsOn: '2027-06-08',
    status: 'Confirmed',
  },
];

describe('Home', () => {
  let fixture: ComponentFixture<Home>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(Home);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function expectHealthRequests(): void {
    http.expectOne('/health').flush({ status: 'Healthy', totalDurationMilliseconds: 1, checks: [] });
    http.expectOne('/health/ready').flush({
      status: 'Healthy',
      totalDurationMilliseconds: 1,
      checks: [],
    });
  }

  function expectTrips(trips: Trip[] = []): void {
    http.expectOne('/api/trips').flush(trips);
  }

  it('should load the health reports', () => {
    fixture.detectChanges();

    expectHealthRequests();
    expectTrips();
    fixture.detectChanges();

    expect(element().textContent).toContain('Healthy');
  });

  it('should reload the health reports when the refresh button is clicked', () => {
    fixture.detectChanges();

    expectHealthRequests();
    expectTrips();
    fixture.detectChanges();

    const refresh = element().querySelector('button') as HTMLButtonElement;
    refresh.click();
    fixture.detectChanges();

    expectHealthRequests();
    fixture.detectChanges();

    expect(element().textContent).toContain('Healthy');
  });

  it('should report when the API cannot be reached', () => {
    fixture.detectChanges();

    http.expectOne('/health').error(new ProgressEvent('error'), { status: 0 });
    http.expectOne('/health/ready').error(new ProgressEvent('error'), { status: 0 });
    http.expectOne('/api/trips').error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();

    expect(element().textContent).toContain('The Tabiji API is not reachable');
  });

  it('should list the journeys', () => {
    fixture.detectChanges();

    expectHealthRequests();
    expectTrips(journeys);
    fixture.detectChanges();

    expect(element().textContent).toContain('Rome & Amalfi');
    expect(element().textContent).toContain('Reykjavík');
    expect(element().querySelectorAll('.journey-list__row')).toHaveLength(2);
  });

  it('should show an empty state when there are no journeys', () => {
    fixture.detectChanges();

    expectHealthRequests();
    expectTrips();
    fixture.detectChanges();

    expect(element().textContent).toContain('No journeys yet');
  });

  it('should render no image', () => {
    fixture.detectChanges();

    expectHealthRequests();
    expectTrips(journeys);
    fixture.detectChanges();

    expect(element().querySelector('img')).toBeNull();
  });
});
