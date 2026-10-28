import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CsrfTokenService } from './csrf-token.service';
import { SessionService } from './session.service';

const account = { id: '5f2b1c4e-0000-0000-0000-000000000000', email: 'traveller@example.com' };

describe('SessionService', () => {
  let service: SessionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should know nobody until it has asked', () => {
    expect(service.isReady()).toBe(false);
    expect(service.isAuthenticated()).toBe(false);
    expect(service.user()).toBeNull();
  });

  it('should take the account from the session probe', () => {
    service.restore();

    http.expectOne('/api/auth/session').flush({ authenticated: true, user: account });

    expect(service.isReady()).toBe(true);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()?.email).toBe('traveller@example.com');
  });

  it('should settle on signed out when the API cannot be reached', () => {
    service.restore();

    http
      .expectOne('/api/auth/session')
      .flush('down', { status: 0, statusText: 'Unknown Error' });

    // The shell must still render: an unreachable API means nobody is known
    // to be signed in, not that the page is broken.
    expect(service.isReady()).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should read the account back after signing in', () => {
    let finished = false;

    service
      .signIn({ email: 'traveller@example.com', password: 'CorrectHorse42' })
      .subscribe(() => (finished = true));

    http.expectOne('/api/auth/login?useCookies=true').flush(null);
    http.expectOne('/api/auth/session').flush({ authenticated: true, user: account });

    expect(finished).toBe(true);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()).toEqual(account);
  });

  it('should forget the antiforgery token when the session changes', () => {
    const csrf = TestBed.inject(CsrfTokenService);

    csrf.load().subscribe();
    http.expectOne('/api/auth/csrf').flush({ token: 'abc' });

    expect(csrf.token()).toBe('abc');

    service
      .signIn({ email: 'traveller@example.com', password: 'CorrectHorse42' })
      .subscribe();

    http.expectOne('/api/auth/login?useCookies=true').flush(null);
    http.expectOne('/api/auth/session').flush({ authenticated: true, user: account });

    // The token carries the account inside it, so it is no longer accepted
    // once the account changes.
    expect(csrf.token()).toBeNull();
  });

  it('should sign out even when the request fails', () => {
    service.restore();
    http.expectOne('/api/auth/session').flush({ authenticated: true, user: account });

    expect(service.isAuthenticated()).toBe(true);

    service.signOut().subscribe();

    http
      .expectOne('/api/auth/logout')
      .flush('nope', { status: 400, statusText: 'Bad Request' });

    expect(service.isAuthenticated()).toBe(false);
  });
});
