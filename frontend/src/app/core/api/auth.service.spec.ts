import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should post the credentials to register', () => {
    service.register({ email: 'a@example.com', password: 'CorrectHorse42' }).subscribe();

    const request = http.expectOne('/api/auth/register');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'a@example.com',
      password: 'CorrectHorse42',
    });

    request.flush(null);
  });

  it('should ask for a cookie rather than a token when signing in', () => {
    service.login({ email: 'a@example.com', password: 'CorrectHorse42' }).subscribe();

    const request = http.expectOne('/api/auth/login?useCookies=true');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'a@example.com',
      password: 'CorrectHorse42',
    });

    request.flush(null);
  });

  it('should post to sign out', () => {
    service.logout().subscribe();

    const request = http.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');

    request.flush(null);
  });

  it('should read the session', () => {
    let authenticated: boolean | undefined;

    service.session().subscribe((session) => (authenticated = session.authenticated));

    const request = http.expectOne('/api/auth/session');
    expect(request.request.method).toBe('GET');

    request.flush({ authenticated: false, user: null });

    expect(authenticated).toBe(false);
  });

  it('should collect the antiforgery token', () => {
    let token: string | undefined;

    service.csrf().subscribe((response) => (token = response.token));

    const request = http.expectOne('/api/auth/csrf');
    expect(request.request.method).toBe('GET');

    request.flush({ token: 'abc' });

    expect(token).toBe('abc');
  });
});
