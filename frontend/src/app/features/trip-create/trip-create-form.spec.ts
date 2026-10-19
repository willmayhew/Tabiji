import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TripCreateForm } from './trip-create-form';

const validTrip = {
  name: 'Tokyo & Kyoto',
  destination: 'Japan',
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

  it('should not call the API when required fields are missing', () => {
    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('This field is required.');
  });

  it('should not call the API when the end date is before the start date', () => {
    fixture.componentInstance.form.setValue({
      ...validTrip,
      startsOn: '2027-04-14',
      endsOn: '2027-04-01',
    });

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('The end date must be on or after the start date.');
  });

  it('should report a name that is too long', () => {
    fixture.componentInstance.form.setValue({
      ...validTrip,
      name: 'x'.repeat(101),
    });

    submit();

    http.expectNone('/api/trips');
    expect(element().textContent).toContain('Use at most 100 characters.');
  });

  it('should create the trip and report success', () => {
    fixture.componentInstance.form.setValue(validTrip);

    submit();

    const request = http.expectOne('/api/trips');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(validTrip);

    request.flush({
      id: '5f2b1c4e-0000-0000-0000-000000000000',
      ...validTrip,
      status: 'Planning',
    });

    fixture.detectChanges();

    expect(element().textContent).toContain('Created "Tokyo & Kyoto".');
    expect(fixture.componentInstance.form.getRawValue()).toEqual({
      name: '',
      destination: '',
      startsOn: '',
      endsOn: '',
    });
  });

  it('should emit the created trip', () => {
    const created: unknown[] = [];
    fixture.componentInstance.created.subscribe((trip) => created.push(trip));

    fixture.componentInstance.form.setValue(validTrip);
    submit();

    http.expectOne('/api/trips').flush({
      id: '5f2b1c4e-0000-0000-0000-000000000000',
      ...validTrip,
      status: 'Planning',
    });

    expect(created).toHaveLength(1);
  });

  it('should show the message from a rejected trip', () => {
    fixture.componentInstance.form.setValue(validTrip);
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
