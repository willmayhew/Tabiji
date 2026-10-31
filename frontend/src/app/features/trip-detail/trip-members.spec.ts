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

  function inviteAddress(email: string): void {
    const input = element().querySelector('#invite-email') as HTMLInputElement;

    input.value = email;
    input.dispatchEvent(new Event('input'));

    element().querySelector('form.members__form')!.dispatchEvent(new Event('submit'));

    http.expectOne('/api/trips/trip-1/invitations').flush({
      ...invitation,
      id: 'inv-2',
      email,
      invitationUrl: 'http://localhost:4200/invite#token=abc',
    });
    fixture.detectChanges();
  }

  it('should show the travellers and the owner controls', () => {
    create('Owner', { viewerRole: 'Owner', members: [owner, editor] });

    expect(element().textContent).toContain('Travellers');
    expect(element().textContent).toContain('owner@example.com');
    expect(element().textContent).toContain('editor@example.com');
    expect(element().textContent).toContain('You');
    expect(element().textContent).toContain('Invite a traveller');
    expect(element().textContent).toContain('Invitations');
    expect(element().textContent).toContain('invited@example.com');

    expect(element().textContent).toContain('1 pending');

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
    expect(element().textContent).not.toContain('Invitations');
    expect(element().querySelector('.member-row__role')).toBeNull();
    expect(element().querySelector('.member-row__actions')).toBeNull();
    http.expectNone('/api/trips/trip-1/invitations');
  });

  it('should show decided invitations as history, with the pending count apart', () => {
    fixture = TestBed.createComponent(TripMembers);
    fixture.componentRef.setInput('tripId', 'trip-1');
    fixture.componentRef.setInput('viewerRole', 'Owner');
    fixture.detectChanges();

    http.expectOne('/api/trips/trip-1/members').flush({ viewerRole: 'Owner', members: [owner] });
    http.expectOne('/api/trips/trip-1/invitations').flush([
      { ...invitation, status: 'Declined' },
      { ...invitation, id: 'inv-2', email: 'pending@example.com', status: 'Pending' },
    ]);
    fixture.detectChanges();

    expect(element().textContent).toContain('Invitations');
    expect(element().textContent).toContain('1 pending');

    // Only the invitation that could still be accepted offers a revoke.
    const revokeButtons = [...element().querySelectorAll('.invitation-row button')];

    expect(revokeButtons.length).toBe(1);
    expect(element().textContent).toContain('pending@example.com');
    expect(element().textContent).toContain('Declined');
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

  it('should copy the development link', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    try {
      create('Owner', { viewerRole: 'Owner', members: [owner, editor] });
      inviteAddress('new@example.com');

      const copy = [...element().querySelectorAll('.members__link button')].find((button) =>
        button.textContent?.includes('Copy link'),
      ) as HTMLButtonElement;

      copy.click();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(writeText).toHaveBeenCalledWith('http://localhost:4200/invite#token=abc');
      expect(copy.textContent).toContain('Copied');
    } finally {
      delete (navigator as { clipboard?: unknown }).clipboard;
    }
  });

  it('should say when the link cannot be copied', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('unavailable')) },
      configurable: true,
    });

    try {
      create('Owner', { viewerRole: 'Owner', members: [owner, editor] });
      inviteAddress('new@example.com');

      const copy = [...element().querySelectorAll('.members__link button')].find((button) =>
        button.textContent?.includes('Copy link'),
      ) as HTMLButtonElement;

      copy.click();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(element().textContent).toContain('The link could not be copied');
    } finally {
      delete (navigator as { clipboard?: unknown }).clipboard;
    }
  });

  it('should explain the controls the last owner cannot use', () => {
    create('Owner', { viewerRole: 'Owner', members: [owner] });

    const select = element().querySelector('.member-row__role') as HTMLSelectElement;
    const remove = element().querySelector('.member-row__actions button') as HTMLButtonElement;
    const hint = element().querySelector('#owner-hint') as HTMLElement;

    expect(select.disabled).toBe(true);
    expect(remove.disabled).toBe(true);
    expect(select.getAttribute('title')).toBe('A trip must keep at least one owner.');
    expect(select.getAttribute('aria-describedby')).toBe('owner-hint');
    expect(remove.getAttribute('aria-describedby')).toBe('owner-hint');
    expect(hint.textContent).toContain('A trip must keep at least one owner.');
  });

  it('should keep a row quiet while its write is in flight', () => {
    const secondEditor: TripMember = {
      userId: 'editor-2',
      email: 'editor2@example.com',
      role: 'Editor',
      grantedAt: '2026-10-30T09:00:00Z',
    };

    create('Owner', { viewerRole: 'Owner', members: [owner, editor, secondEditor] });

    const promotedSelect = element().querySelectorAll('.member-row__role')[1] as HTMLSelectElement;

    promotedSelect.value = 'Owner';
    promotedSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const request = http.expectOne(`/api/trips/trip-1/members/${editor.userId}`);
    const row = promotedSelect.closest('.member-row') as HTMLElement;
    const otherSelect = element().querySelectorAll('.member-row__role')[2] as HTMLSelectElement;

    // Only the row being written is locked.
    expect(row.querySelector('select')?.disabled).toBe(true);
    expect(row.querySelector('button')?.disabled).toBe(true);
    expect(otherSelect.disabled).toBe(false);

    request.flush({
      viewerRole: 'Owner',
      members: [owner, { ...editor, role: 'Owner' }, secondEditor],
    });
    fixture.detectChanges();

    expect(promotedSelect.disabled).toBe(false);
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
