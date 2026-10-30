import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SessionService } from '../../core/auth/session.service';
import { TripMember, TripMembers as TripMembersView } from '../../core/models/member';
import { TripMembers } from './trip-members';

const owner: TripMember = {
  userId: 'owner-1',
  email: 'owner@example.com',
  role: 'Owner',
  grantedAt: '2026-10-30T09:00:00Z',
};

const editor: TripMember = {
  userId: 'editor-1',
  email: 'editor@example.com',
  role: 'Editor',
  grantedAt: '2026-10-30T09:00:00Z',
};

const invitation = {
  id: 'inv-1',
  tripId: 'trip-1',
  email: 'invited@example.com',
  role: 'Editor' as const,
  status: 'Pending' as const,
  createdAt: '2026-10-30T09:00:00Z',
  expiresAt: '2026-11-06T09:00:00Z',
  invitedByEmail: 'owner@example.com',
};

function session() {
  return {
    user: signal({ id: 'owner-1', email: 'owner@example.com' }),
    markSignedOut: vi.fn(),
  };
}

describe('TripMembers', () => {
  let fixture: ComponentFixture<TripMembers>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    await TestBed.configureTestingModule({
      imports: [TripMembers],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: session() },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function create(role: 'Owner' | 'Editor', members: TripMembersView): void {
    fixture = TestBed.createComponent(TripMembers);
    fixture.componentRef.setInput('tripId', 'trip-1');
    fixture.componentRef.setInput('viewerRole', role);
    fixture.detectChanges();

    http.expectOne('/api/trips/trip-1/members').flush(members);

    if (role === 'Owner') {
      http.expectOne('/api/trips/trip-1/invitations').flush([invitation]);
    }

    fixture.detectChanges();
  }

  it('should show the travellers and the owner controls', () => {
    create('Owner', { viewerRole: 'Owner', members: [owner, editor] });

    expect(element().textContent).toContain('Travellers');
    expect(element().textContent).toContain('owner@example.com');
    expect(element().textContent).toContain('editor@example.com');
    expect(element().textContent).toContain('You');
    expect(element().textContent).toContain('Invite a traveller');
    expect(element().textContent).toContain('Pending invitations');
    expect(element().textContent).toContain('invited@example.com');

    const selects = element().querySelectorAll('.member-row__role');

    expect(selects.length).toBe(2);

    // The one owner cannot be demoted or removed.
    const ownerSelect = selects[0] as HTMLSelectElement;

    expect(ownerSelect.disabled).toBe(true);
    expect(element().textContent).toContain('A trip must keep at least one owner.');
  });

  it('should show only the members to an editor', () => {
    create('Editor', { viewerRole: 'Editor', members: [owner, editor] });

    expect(element().textContent).toContain('editor@example.com');
    expect(element().textContent).not.toContain('Invite a traveller');
    expect(element().textContent).not.toContain('Pending invitations');
    expect(element().querySelector('.member-row__role')).toBeNull();
    expect(element().querySelector('.member-row__actions')).toBeNull();
    http.expectNone('/api/trips/trip-1/invitations');
  });

  it('should invite an address and show the development link', () => {
    create('Owner', { viewerRole: 'Owner', members: [owner, editor] });

    const input = element().querySelector('#invite-email') as HTMLInputElement;

    input.value = 'new@example.com';
    input.dispatchEvent(new Event('input'));

    element().querySelector('form.members__form')!.dispatchEvent(new Event('submit'));

    const request = http.expectOne('/api/trips/trip-1/invitations');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'new@example.com' });

    request.flush({
      ...invitation,
      id: 'inv-2',
      email: 'new@example.com',
      invitationUrl: 'http://localhost:4200/invite#token=abc',
    });
    fixture.detectChanges();

    expect(element().textContent).toContain('Invitation sent to new@example.com');

    const link = element().querySelector('a.link-arrow') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('http://localhost:4200/invite#token=abc');
  });

  it('should revoke a pending invitation', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    create('Owner', { viewerRole: 'Owner', members: [owner, editor] });

    const revoke = [...element().querySelectorAll('.invitation-row button')].find((button) =>
      button.textContent?.includes('Revoke'),
    ) as HTMLButtonElement;

    revoke.click();

    const request = http.expectOne(`/api/trips/trip-1/invitations/${invitation.id}`);

    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    fixture.detectChanges();

    expect(element().querySelectorAll('.invitation-row')).toHaveLength(0);
    expect(element().textContent).toContain('Revoked the invitation to invited@example.com.');
  });

  it('should promote an editor and report the updated members', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    create('Owner', { viewerRole: 'Owner', members: [owner, editor] });

    const editorSelect = element().querySelectorAll('.member-row__role')[1] as HTMLSelectElement;

    editorSelect.value = 'Owner';
    editorSelect.dispatchEvent(new Event('change'));

    const request = http.expectOne(`/api/trips/trip-1/members/${editor.userId}`);

    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({ role: 'Owner' });
    request.flush({
      viewerRole: 'Owner',
      members: [owner, { ...editor, role: 'Owner' }],
    });
    fixture.detectChanges();

    expect(element().textContent).toContain('editor@example.com is now an owner');
  });
});
