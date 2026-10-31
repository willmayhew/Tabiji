import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { SessionService } from '../../core/auth/session.service';
import { Invitation } from './invitation';

function session(authenticated: boolean) {
  return {
    isReady: signal(true),
    isAuthenticated: signal(authenticated),
    user: signal(authenticated ? { id: 'user-1', email: 'user@example.com' } : null),
    markSignedOut: vi.fn(),
  };
}

function route(fragment: string | null) {
  return {
    snapshot: {
      fragment,
      queryParamMap: convertToParamMap({}),
    },
  };
}

const preview = {
  tripId: 'trip-1',
  tripName: 'Japan',
  invitedByEmail: 'owner@example.com',
  role: 'Editor' as const,
  status: 'Pending' as const,
  expiresAt: '2026-11-06T09:00:00Z',
  canAccept: true,
};

describe('Invitation', () => {
  let fixture: ComponentFixture<Invitation>;
  let http: HttpTestingController;

  async function configure(authenticated: boolean, fragment: string | null): Promise<void> {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [Invitation],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: route(fragment) },
        { provide: SessionService, useValue: session(authenticated) },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Invitation);
    http = TestBed.inject(HttpTestingController);
  }

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function showPending(): void {
    fixture.detectChanges();

    http.expectOne('/api/invitations/preview').flush(preview);
    fixture.detectChanges();
  }

  it('should preview the invitation and accept it', async () => {
    await configure(true, 'token=abc');

    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');

    fixture.detectChanges();

    const request = http.expectOne('/api/invitations/preview');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'abc' });
    request.flush(preview);
    fixture.detectChanges();

    expect(element().textContent).toContain('Japan');
    expect(element().textContent).toContain('owner@example.com');
    expect(element().textContent).toContain('Accept invitation');

    const accept = element().querySelector('.invitation__actions .button') as HTMLButtonElement;

    accept.click();

    const decision = http.expectOne('/api/invitations/accept');

    expect(decision.request.body).toEqual({ token: 'abc' });
    decision.flush({ tripId: 'trip-1', role: 'Editor' });

    expect(navigate).toHaveBeenCalledWith(['/trips', 'trip-1']);
  });

  it('should decline the invitation', async () => {
    await configure(true, 'token=abc');
    showPending();

    const decline = element().querySelector(
      '.invitation__actions .button--secondary',
    ) as HTMLButtonElement;

    decline.click();

    const decision = http.expectOne('/api/invitations/decline');

    expect(decision.request.body).toEqual({ token: 'abc' });
    decision.flush(null);
    fixture.detectChanges();

    expect(element().textContent).toContain('Invitation declined');
  });

  it('should ask a signed-out reader to sign in and come back to the link', async () => {
    await configure(false, 'token=abc');

    fixture.detectChanges();

    http.expectNone('/api/invitations/preview');

    expect(element().textContent).toContain('Sign in to accept this invitation');

    const link = element().querySelector('app-sign-in-prompt a.button') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/login?returnUrl=%2Finvite%23token%3Dabc');

    // A reader with no account is offered one, without losing the invitation.
    const register = element().querySelector(
      'app-sign-in-prompt a:not(.button)',
    ) as HTMLAnchorElement;

    expect(register.getAttribute('href')).toBe('/register?returnUrl=%2Finvite%23token%3Dabc');
  });

  it('should not reveal an invitation that is not for this account', async () => {
    await configure(true, 'token=abc');

    fixture.detectChanges();

    http
      .expectOne('/api/invitations/preview')
      .flush('not found', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(element().textContent).toContain('This invitation is not for this account');
    expect(element().querySelector('button')).toBeNull();
  });

  it('should show a terminal state without offering a decision', async () => {
    await configure(true, 'token=abc');

    fixture.detectChanges();

    http.expectOne('/api/invitations/preview').flush({
      ...preview,
      status: 'Revoked',
      canAccept: false,
    });
    fixture.detectChanges();

    expect(element().textContent).toContain('was revoked');
    expect(element().querySelector('.invitation__actions')).toBeNull();
  });

  it('should reject a link with no token without asking the API', async () => {
    await configure(true, null);

    fixture.detectChanges();

    http.expectNone('/api/invitations/preview');

    expect(element().textContent).toContain('This invitation is not for this account');
  });

  it('should offer a retry when the preview fails', async () => {
    await configure(true, 'token=abc');

    fixture.detectChanges();

    http
      .expectOne('/api/invitations/preview')
      .flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(element().textContent).toContain('Something went wrong');

    (element().querySelector('.empty-state .button') as HTMLButtonElement).click();

    http.expectOne('/api/invitations/preview').flush(preview);
    fixture.detectChanges();

    expect(element().textContent).toContain('Japan');
  });
});
