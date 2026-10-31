import { ACCOUNTS, api, expect, test } from './fixtures';
import { createTrip, invite, makeEditor, membersOf, tokenOf } from './helpers';

/**
 * What a shared trip looks like from the editor's side: everything the
 * journey needs, and nothing the owner keeps. The API assertions use the
 * editor's own session, so they prove the server's answer rather than the
 * screen's choice of buttons.
 */
test.describe('an editor on a shared trip', () => {
  test('EDI-01 the shared trip appears in the editor list', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Shared journey');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await editorContext.newPage();
    await page.goto('/trips');

    // The shared trip is listed like any of the editor's own.
    await expect(page.locator('a.trip-link', { hasText: 'Shared journey' })).toBeVisible();
  });

  test('EDI-02 the editor changes the trip and it survives a reload', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Shared journey');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await editorContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Shared journey' }).waitFor();

    await page.getByRole('button', { name: 'Edit trip' }).click();
    await expect(page.getByRole('heading', { name: 'Edit trip' })).toBeVisible();

    await page.locator('#trip-name').fill('Shared journey, renamed');
    await page.getByRole('button', { name: 'Save changes' }).click();

    await expect(page.getByRole('heading', { name: 'Shared journey, renamed' })).toBeVisible();

    await page.reload();
    await expect(page.getByRole('heading', { name: 'Shared journey, renamed' })).toBeVisible();
  });

  test('EDI-03 the editor cannot delete the trip', async ({ ownerContext, editorContext }) => {
    const trip = await createTrip(ownerContext, 'Shared journey');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await editorContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('button', { name: 'Edit trip' }).click();

    // The screen never offers it...
    await expect(page.getByRole('button', { name: 'Delete this trip' })).toHaveCount(0);

    // ...and the API refuses it when asked directly.
    const response = await api(editorContext, 'DELETE', `/api/trips/${trip.id}`);
    expect(response.status()).toBe(403);
  });

  test('EDI-04 the editor cannot manage the membership', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Shared journey');
    await makeEditor(ownerContext, editorContext, trip.id);

    const members = await membersOf(ownerContext, trip.id);
    const ownerId = members.members.find((member) => member.role === 'Owner')?.userId;
    expect(ownerId).toBeTruthy();

    const invited = await api(editorContext, 'POST', `/api/trips/${trip.id}/invitations`, {
      email: ACCOUNTS.stranger.email,
    });
    expect(invited.status()).toBe(403);

    const promoted = await api(editorContext, 'PATCH', `/api/trips/${trip.id}/members/${ownerId}`, {
      role: 'Editor',
    });
    expect(promoted.status()).toBe(403);

    const removed = await api(editorContext, 'DELETE', `/api/trips/${trip.id}/members/${ownerId}`);
    expect(removed.status()).toBe(403);
  });

  test('EDI-05 a reload keeps the editor role', async ({ ownerContext, editorContext }) => {
    const trip = await createTrip(ownerContext, 'Shared journey');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await editorContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Travellers' }).waitFor();

    await page.reload();

    await expect(page.getByRole('heading', { name: 'Travellers' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Invite a traveller' })).toHaveCount(0);
    await expect(page.locator('.member-row__role')).toHaveCount(0);
  });

  test('EDI-06 the editor has no way to leave from the screen', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Shared journey');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await editorContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Travellers' }).waitFor();

    const row = page.locator('.member-row', { hasText: ACCOUNTS.editor.email });
    await expect(row).toBeVisible();
    await expect(row.getByRole('button')).toHaveCount(0);
  });

  test('EDI-07 an invitation the editor was never named in stays closed', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Shared journey');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.stranger.email);

    const page = await editorContext.newPage();
    await page.goto(invitation.invitationUrl!);

    await expect(
      page.getByRole('heading', { name: 'This invitation is not for this account' }),
    ).toBeVisible();

    // The token itself is not enough: the address has to match too.
    const preview = await api(editorContext, 'POST', '/api/invitations/preview', {
      token: tokenOf(invitation.invitationUrl!),
    });
    expect(preview.status()).toBe(404);
  });
});
