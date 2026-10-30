import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SessionService } from '../../core/auth/session.service';
import { Trip } from '../../core/models/trip';
import { TripCreate } from './trip-create';

/** A stand-in for the session, so the screen's state is chosen by the test. */
function session(authenticated: boolean) {
  return {
    isReady: signal(true),
    isAuthenticated: signal(authenticated),
    user: signal(authenticated ? { id: 'user-1', email: 'traveller@example.com' } : null),
    markSignedOut: vi.fn(),
  };
}

const created: Trip = {
  id: '5f2b1c4e-0000-0000-0000-000000000000',
  name: 'Rome & Amalfi',
  destinations: [
    { id: 'a1', name: 'Rome', sortOrder: 0, startsOn: null, endsOn: null, latitude: null, longitude: null },
    { id: 'b2', name: 'Amalfi', sortOrder: 1, startsOn: null, endsOn: null, latitude: null, longitude: null },
  ],
  startsOn: '2027-04-01',
  endsOn: '2027-04-14',
  status: 'Planning',
};

describe('TripCreate', () => {
  let fixture: ComponentFixture<TripCreate>;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [TripCreate],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SessionService, useValue: session(true) },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TripCreate);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('should render the create form', () => {
    expect(element().querySelector('form')).not.toBeNull();
    expect(element().querySelector('#trip-name')).not.toBeNull();
    expect(element().querySelector('#trip-destination-0')).not.toBeNull();
    expect(element().querySelector('#trip-starts-on')).not.toBeNull();
    expect(element().querySelector('#trip-ends-on')).not.toBeNull();
  });

  it('should label every field', () => {
    const labels = [...element().querySelectorAll('label')] as HTMLLabelElement[];

    // Three fixed fields, plus one for each stop of the route.
    const stops = element().querySelectorAll('.route__row').length;

    expect(labels.length).toBe(3 + stops);

    for (const label of labels) {
      const target = element().querySelector(`#${label.getAttribute('for')}`);

      expect(target).not.toBeNull();
    }
  });

  it('should number the field groups', () => {
    const indexes = [...element().querySelectorAll('.fieldset__index')] as HTMLElement[];

    expect(indexes.length).toBe(3);
    expect(indexes.map((index) => index.textContent?.trim())).toEqual(['01', '02', '03']);
  });

  it('should not assume the trip is in Japan', () => {
    const note = element().querySelector('.create__note')?.textContent ?? '';

    expect(note).toContain('Rome');
    expect(note).not.toContain('Kyoto');
  });

  it('should link back to the trips list', () => {
    const back = element().querySelector('.back-link') as HTMLAnchorElement;

    expect(back.getAttribute('href')).toBe('/trips');
  });

  it('should navigate to the created trip', async () => {
    vi.useFakeTimers();
    const navigate = vi.spyOn(router, 'navigate');

    const form = element().querySelector('form') as HTMLFormElement;
    const input = element().querySelector('#trip-name') as HTMLInputElement;

    input.value = created.name;
    input.dispatchEvent(new Event('input'));

    const destination = element().querySelector('#trip-destination-0') as HTMLInputElement;
    destination.value = created.destinations[0].name;
    destination.dispatchEvent(new Event('input'));

    const startsOn = element().querySelector('#trip-starts-on') as HTMLInputElement;
    startsOn.value = created.startsOn;
    startsOn.dispatchEvent(new Event('input'));

    const endsOn = element().querySelector('#trip-ends-on') as HTMLInputElement;
    endsOn.value = created.endsOn;
    endsOn.dispatchEvent(new Event('input'));

    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const request = http.expectOne('/api/trips');
    expect(request.request.method).toBe('POST');

    request.flush(created);
    fixture.detectChanges();

    vi.advanceTimersByTime(1500);

    expect(navigate).toHaveBeenCalledWith(['/trips', created.id]);

    vi.useRealTimers();
  });
});

describe('TripCreate — signed out', () => {
  let fixture: ComponentFixture<TripCreate>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [TripCreate],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: SessionService, useValue: session(false) },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TripCreate);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('should invite a sign-in instead of showing the form', () => {
    expect(element().querySelector('form')).toBeNull();
    expect(element().textContent).toContain('Sign in to carry on');
    expect(element().textContent).toContain('Sign in to start a trip');

    const link = element().querySelector('app-sign-in-prompt a.button') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/login?returnUrl=%2Ftrips%2Fnew');
  });
});
