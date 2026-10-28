import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { Login } from './login';

const account = { id: 'id', email: 'traveller@example.com' };

let http: HttpTestingController;
let router: Router;

async function configure(queryParams: Record<string, string> = {}): Promise<void> {
  await TestBed.configureTestingModule({
    imports: [Login],
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
      },
    ],
  }).compileComponents();

  http = TestBed.inject(HttpTestingController);
  router = TestBed.inject(Router);
}

function create(): ComponentFixture<Login> {
  const fixture = TestBed.createComponent(Login);
  fixture.detectChanges();

  return fixture;
}

function element(fixture: ComponentFixture<Login>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function submit(fixture: ComponentFixture<Login>): void {
  (element(fixture).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
}

function signIn(fixture: ComponentFixture<Login>): void {
  fixture.componentInstance.form.setValue({
    email: 'traveller@example.com',
    password: 'CorrectHorse42',
  });

  submit(fixture);

  http.expectOne('/api/auth/login?useCookies=true').flush(null);
  http.expectOne('/api/auth/session').flush({ authenticated: true, user: account });
}

describe('Login', () => {
  beforeEach(() => configure());

  afterEach(() => http.verify());

  it('should not send a form that is empty', () => {
    const fixture = create();

    submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/login?useCookies=true');
    expect(element(fixture).querySelector('.field-error')?.textContent).toContain(
      'This field is required.',
    );
  });

  it('should say one thing when the credentials are refused', () => {
    const fixture = create();

    fixture.componentInstance.form.setValue({ email: 'a@example.com', password: 'wrong' });

    submit(fixture);

    http
      .expectOne('/api/auth/login?useCookies=true')
      .flush('', { status: 401, statusText: 'Unauthorized' });

    fixture.detectChanges();

    // It never says which of the two was wrong.
    expect(element(fixture).querySelector('.field-error--api')?.textContent).toContain(
      'The email or password is incorrect.',
    );
  });

  it('should go back to the overview after signing in', () => {
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    const fixture = create();

    signIn(fixture);
    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith('/');
  });
});

describe('Login asked to return somewhere', () => {
  beforeEach(() => configure({ returnUrl: '/trips' }));

  afterEach(() => http.verify());

  it('should return to the page that asked for a sign-in', () => {
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    const fixture = create();

    signIn(fixture);
    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith('/trips');
  });
});
