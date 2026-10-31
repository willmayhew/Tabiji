import { ACCOUNTS, api, expect, test } from './fixtures';
import { createTrip, invite, makeEditor, membersOf, tokenOf } from './helpers';

/**
 * The owner's side of the membership: who is on the trip, what they are,
 * and what the owner may change about them. Every test starts from a trip
 * of its own, so a promotion in one test never leaks into the next.
 */
test.describe('the travellers panel', () => {
  test('MEM-01 the owner sees the editors and the count', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Kyoto in spring');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);

    await page.getByRole('heading', { name: 'Travellers' }).waitFor();
    await expect(page.getByText('2 people')).toBeVisible();

    const row = page.locator('.member-row', { hasText: ACCOUNTS.editor.email });
    await expect(row.locator('app-status-badge', { hasText: 'Editor' })).toBeVisible();
    await expect(row.locator('app-status-badge', { hasText: 'Owner' })).toHaveCount(0);
  });

  test('MEM-02 the owner promotes an editor to owner', async ({ ownerContext, editorContext }) => {
    const trip = await createTrip(ownerContext, 'Osaka food run');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Invite a traveller' }).waitFor();

    const row = page.locator('.member-row', { hasText: ACCOUNTS.editor.email });
    await row.locator('.member-row__role').selectOption('Owner');

    await expect(row.locator('app-status-badge', { hasText: 'Owner' })).toBeVisible();

    // The API agrees, not just the screen.
    const members = await membersOf(ownerContext, trip.id);
    expect(members.members.find((member) => member.email === ACCOUNTS.editor.email)?.role).toBe(
      'Owner',
    );
  });

  test('MEM-03 the owner demotes an owner, after saying so', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Nara day trip');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Invite a traveller' }).waitFor();

    // First put the editor up, so the demote below has a second owner to take.
    await page
      .locator('.member-row', { hasText: ACCOUNTS.editor.email })
      .locator('.member-row__role')
      .selectOption('Owner');
    await expect(
      page
        .locator('.member-row', { hasText: ACCOUNTS.editor.email })
        .locator('app-status-badge', { hasText: 'Owner' }),
    ).toBeVisible();

    let asked: string | null = null;
    page.on('dialog', (dialog) => {
      asked = dialog.message();
      void dialog.accept();
    });

    await page
      .locator('.member-row', { hasText: ACCOUNTS.editor.email })
      .locator('.member-row__role')
      .selectOption('Editor');

    await expect(asked).toContain(ACCOUNTS.editor.email);
    await expect(
      page
        .locator('.member-row', { hasText: ACCOUNTS.editor.email })
        .locator('app-status-badge', { hasText: 'Editor' }),
    ).toBeVisible();
  });

  test('MEM-04 a demote the owner calls off changes nothing', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Kobe beef itinerary');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Invite a traveller' }).waitFor();

    await page
      .locator('.member-row', { hasText: ACCOUNTS.editor.email })
      .locator('.member-row__role')
      .selectOption('Owner');
    await expect(
      page
        .locator('.member-row', { hasText: ACCOUNTS.editor.email })
        .locator('app-status-badge', { hasText: 'Owner' }),
    ).toBeVisible();

    page.once('dialog', (dialog) => void dialog.dismiss());

    await page
      .locator('.member-row', { hasText: ACCOUNTS.editor.email })
      .locator('.member-row__role')
      .selectOption('Editor');

    // The select keeps the role it had, and no write happened.
    await expect(
      page
        .locator('.member-row', { hasText: ACCOUNTS.editor.email })
        .locator('app-status-badge', { hasText: 'Owner' }),
    ).toBeVisible();

    const members = await membersOf(ownerContext, trip.id);
    expect(members.members.find((member) => member.email === ACCOUNTS.editor.email)?.role).toBe(
      'Owner',
    );
  });

  test('MEM-05 the last owner is told why their controls are locked', async ({ ownerContext }) => {
    const trip = await createTrip(ownerContext, 'Hakone weekend');

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Invite a traveller' }).waitFor();

    const row = page.locator('.member-row', { hasText: ACCOUNTS.owner.email });
    const select = row.locator('.member-row__role');
    const remove = row.getByRole('button', { name: /Remove/ });

    await expect(select).toBeDisabled();
    await expect(remove).toBeDisabled();
    await expect(page.locator('#owner-hint')).toBeVisible();
    expect(await select.getAttribute('title')).toBe('A trip must keep at least one owner.');
    expect(await select.getAttribute('aria-describedby')).toBe('owner-hint');
    expect(await remove.getAttribute('aria-describedby')).toBe('owner-hint');
  });

  test('MEM-06 removing an editor ends their access at once', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Kanazawa museums');
    await makeEditor(ownerContext, editorContext, trip.id);

    const ownerPage = await ownerContext.newPage();
    await ownerPage.goto(`/trips/${trip.id}`);
    await ownerPage.getByRole('heading', { name: 'Invite a traveller' }).waitFor();

    let asked: string | null = null;
    ownerPage.on('dialog', (dialog) => {
      asked = dialog.message();
      void dialog.accept();
    });

    await ownerPage
      .locator('.member-row', { hasText: ACCOUNTS.editor.email })
      .getByRole('button', { name: /Remove/ })
      .click();

    await expect(asked).toContain(ACCOUNTS.editor.email);
    await expect(ownerPage.locator('.member-row', { hasText: ACCOUNTS.editor.email })).toHaveCount(
      0,
    );

    // The editor's next look at the trip is the not-found state, exactly as
    // a trip that never existed.
    const editorPage = await editorContext.newPage();
    await editorPage.goto(`/trips/${trip.id}`);
    await expect(
      editorPage.getByRole('heading', { name: 'This trip has wandered off' }),
    ).toBeVisible();

    const trips = await editorContext.newPage();
    await trips.goto('/trips');
    await expect(trips.getByText('Kanazawa museums')).toHaveCount(0);
  });

  test('MEM-07 revoking an invitation takes the link with it', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Tottori sand dunes');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.editor.email);

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Invitations' }).waitFor();

    let asked: string | null = null;
    page.on('dialog', (dialog) => {
      asked = dialog.message();
      void dialog.accept();
    });

    await page
      .locator('.invitation-row', { hasText: ACCOUNTS.editor.email })
      .getByRole('button', { name: 'Revoke' })
      .click();

    await expect(asked).toContain(ACCOUNTS.editor.email);
    await expect(page.locator('.invitation-row', { hasText: ACCOUNTS.editor.email })).toHaveCount(
      0,
    );

    // The history keeps the row, but a revoked invitation cannot be acted on.
    await page.reload();
    await expect(page.locator('.invitation-row', { hasText: ACCOUNTS.editor.email })).toBeVisible();
    await expect(page.locator('app-status-badge', { hasText: 'Revoked' })).toBeVisible();
    await expect(
      page.locator('.invitation-row', { hasText: ACCOUNTS.editor.email }).getByRole('button'),
    ).toHaveCount(0);

    const preview = await api(editorContext, 'POST', '/api/invitations/preview', {
      token: tokenOf(invitation.invitationUrl!),
    });
    expect(preview.status()).toBe(200);
    expect(await preview.json()).toMatchObject({ status: 'Revoked', canAccept: false });
  });

  test('MEM-08 an editor sees the people and none of the controls', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Hiroshima peace park');
    await makeEditor(ownerContext, editorContext, trip.id);

    const requested: string[] = [];
    editorContext.on('request', (request) => requested.push(request.url()));

    const page = await editorContext.newPage();
    await page.goto(`/trips/${trip.id}`);

    await page.getByRole('heading', { name: 'Travellers' }).waitFor();
    await expect(page.locator('.member-row', { hasText: ACCOUNTS.owner.email })).toBeVisible();

    await expect(page.getByRole('heading', { name: 'Invite a traveller' })).toHaveCount(0);
    await expect(page.locator('.member-row__role')).toHaveCount(0);
    await expect(page.getByRole('button', { name: /Remove/ })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Invitations' })).toHaveCount(0);

    // The panel never even asks for the owner's invitation list.
    expect(requested.some((url) => url.includes('/invitations'))).toBe(false);
  });

  test('MEM-09 decided invitations stay as history, apart from the pending count', async ({
    ownerContext,
    strangerContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Mount Fuji dawn');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.stranger.email);
    const declined = await api(strangerContext, 'POST', '/api/invitations/decline', {
      token: tokenOf(invitation.invitationUrl!),
    });
    expect(declined.status()).toBe(204);

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Invitations' }).waitFor();

    // Nothing is pending, so no count is claimed.
    await expect(page.getByText('pending')).toHaveCount(0);
    await expect(
      page.locator('.invitation-row', { hasText: ACCOUNTS.stranger.email }),
    ).toBeVisible();
    await expect(page.locator('app-status-badge', { hasText: 'Declined' })).toBeVisible();
  });
});
