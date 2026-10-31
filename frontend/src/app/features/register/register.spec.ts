import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { Register } from './register';

describe('Register', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  function create(): ComponentFixture<Register> {
    const fixture = TestBed.createComponent(Register);
    fixture.detectChanges();

    return fixture;
  }

  function element(fixture: ComponentFixture<Register>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function submit(fixture: ComponentFixture<Register>): void {
    (element(fixture).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
  }

  function fill(
    fixture: ComponentFixture<Register>,
    values: { email: string; password: string; confirmPassword: string },
  ): void {
    fixture.componentInstance.form.setValue(values);
  }

  it('should refuse two different passwords without asking the API', () => {
    const fixture = create();

    fill(fixture, {
      email: 'traveller@example.com',
      password: 'CorrectHorse42',
      confirmPassword: 'DifferentHorse42',
    });

    submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/register');
    expect(element(fixture).querySelector('#register-confirmation-error')?.textContent).toContain(
      'The two passwords do not match.',
    );
  });

  it('should refuse a password that does not meet the rule', () => {
    const fixture = create();

    fill(fixture, {
      email: 'traveller@example.com',
      password: 'short',
      confirmPassword: 'short',
    });

    submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/register');
    expect(element(fixture).querySelector('#register-password-error')?.textContent).toContain(
      'at least 10 characters',
    );
  });

  it('should put a duplicate address on the email field', () => {
    const fixture = create();

    fill(fixture, {
      email: 'traveller@example.com',
      password: 'CorrectHorse42',
      confirmPassword: 'CorrectHorse42',
    });

    submit(fixture);

    http.expectOne('/api/auth/register').flush(
      {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { DuplicateEmail: ['The email is already taken.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );

    fixture.detectChanges();

    expect(element(fixture).querySelector('#register-email-error')?.textContent).toContain(
      'An account already uses this address.',
    );
  });

  it('should go to the sign-in screen once the account exists', () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = create();

    fill(fixture, {
      email: 'traveller@example.com',
      password: 'CorrectHorse42',
      confirmPassword: 'CorrectHorse42',
    });

    submit(fixture);

    http.expectOne('/api/auth/register').flush(null);

    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { registered: 'true' },
    });
  });
});

describe('Register with a return URL', () => {
  let http: HttpTestingController;
  let router: Router;
  let requested: string | null;

  beforeEach(async () => {
    requested = null;

    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useFactory: () => ({
            snapshot: {
              queryParamMap: convertToParamMap(requested ? { returnUrl: requested } : {}),
            },
          }),
        },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  function create(): ComponentFixture<Register> {
    const fixture = TestBed.createComponent(Register);
    fixture.detectChanges();

    return fixture;
  }

  function element(fixture: ComponentFixture<Register>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function submit(fixture: ComponentFixture<Register>): void {
    (element(fixture).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
  }

  function fill(fixture: ComponentFixture<Register>): void {
    fixture.componentInstance.form.setValue({
      email: 'traveller@example.com',
      password: 'CorrectHorse42',
      confirmPassword: 'CorrectHorse42',
    });
  }

  it('should carry an invitation through to the sign-in screen', () => {
    requested = '/invite#token=abc';
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = create();

    fill(fixture);
    submit(fixture);

    http.expectOne('/api/auth/register').flush(null);

    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { registered: 'true', returnUrl: '/invite#token=abc' },
    });
  });

  it('should drop a return URL that leads off the site', () => {
    requested = '//elsewhere.example/trip';
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = create();

    fill(fixture);
    submit(fixture);

    http.expectOne('/api/auth/register').flush(null);

    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { registered: 'true' },
    });
  });
});
