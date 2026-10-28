import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { csrfInterceptor } from './core/auth/csrf.interceptor';
import { Session } from './core/models/session';

/** A page with nothing on it, so the shell can be tested without the screens. */
@Component({ selector: 'app-test-page', template: '' })
class TestPage {}

const anonymous: Session = { authenticated: false, user: null };

const signedIn: Session = {
  authenticated: true,
  user: { id: '5f2b1c4e-0000-0000-0000-000000000000', email: 'traveller@example.com' },
};

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([
          { path: '', component: TestPage },
          { path: 'trips', component: TestPage },
          { path: '**', redirectTo: '' },
        ]),
        provideHttpClient(withInterceptors([csrfInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /**
   * The shell asks the API who the reader is as soon as it is made. Every
   * test has to answer that question.
   */
  function answerSession(session: Session): void {
    http.expectOne('/api/auth/session').flush(session);
  }

  it('should create the app', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(anonymous);
    await fixture.whenStable();
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the brand', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(anonymous);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand-name')?.textContent).toContain('Tabiji');
  });

  it('should offer a skip link as the first focusable element', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(anonymous);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;

    const skipLink = compiled.querySelector('.skip-link') as HTMLAnchorElement;

    expect(skipLink.getAttribute('href')).toBe('#main-content');
    expect(compiled.querySelector('main')?.getAttribute('id')).toBe('main-content');
  });

  it('should mark the active navigation link for assistive technology', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(anonymous);
    await fixture.whenStable();

    const router = TestBed.inject(Router);
    await router.navigateByUrl('/trips');
    fixture.detectChanges();

    const links = [...(fixture.nativeElement as HTMLElement).querySelectorAll('.app-nav a')];

    expect(links.length).toBe(2);

    const trips = links.find((link) => link.textContent?.trim() === 'Trips') as HTMLAnchorElement;

    expect(trips.getAttribute('aria-current')).toBe('page');
    expect(trips.classList.contains('is-active')).toBe(true);
  });

  it('should offer a new trip action in the header', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(anonymous);
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const action = element.querySelector('.app-header__actions a') as HTMLAnchorElement;

    expect(action).not.toBeNull();
    expect(action.getAttribute('href')).toBe('/trips/new');
  });

  it('should keep the navigation free of the header action', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(anonymous);
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelectorAll('.app-nav a')).toHaveLength(2);
    expect(element.querySelectorAll('.app-nav .button')).toHaveLength(0);
  });

  it('should offer a way to sign in when nobody is', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(anonymous);
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const signIn = element.querySelector('.app-header__actions a[href="/login"]');

    expect(signIn).not.toBeNull();
    expect(signIn?.textContent?.trim()).toBe('Sign in');
  });

  it('should show who is signed in and offer to sign out', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(signedIn);
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.app-header__account')?.textContent).toContain(
      'traveller@example.com',
    );
    expect(element.querySelector('.app-header__actions button')?.textContent).toContain('Sign out');
    expect(element.querySelector('.app-header__actions a[href="/login"]')).toBeNull();
  });

  it('should sign out when the header action is pressed', async () => {
    const fixture = TestBed.createComponent(App);
    answerSession(signedIn);
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const signOut = element.querySelector('.app-header__actions button') as HTMLButtonElement;

    signOut.click();

    // Signing out carries the antiforgery token, so the token is collected
    // first, then the request is sent.
    http.expectOne('/api/auth/csrf').flush({ token: 'abc' });

    const request = http.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('abc');
    request.flush(null);

    fixture.detectChanges();

    expect(element.querySelector('.app-header__actions a[href="/login"]')).not.toBeNull();
  });
});
