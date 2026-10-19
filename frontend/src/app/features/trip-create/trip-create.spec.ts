import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Trip } from '../../core/models/trip';
import { TripCreate } from './trip-create';

const created: Trip = {
  id: '5f2b1c4e-0000-0000-0000-000000000000',
  name: 'Tokyo & Kyoto',
  destination: 'Japan',
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
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
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
    expect(element().querySelector('#trip-destination')).not.toBeNull();
    expect(element().querySelector('#trip-starts-on')).not.toBeNull();
    expect(element().querySelector('#trip-ends-on')).not.toBeNull();
  });

  it('should label every field', () => {
    const labels = [...element().querySelectorAll('label')] as HTMLLabelElement[];

    expect(labels.length).toBe(4);

    for (const label of labels) {
      const target = element().querySelector(`#${label.getAttribute('for')}`);

      expect(target).not.toBeNull();
    }
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

    const destination = element().querySelector('#trip-destination') as HTMLInputElement;
    destination.value = created.destination;
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
