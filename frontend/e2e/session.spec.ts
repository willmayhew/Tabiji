import { ACCOUNTS, expect, test } from './fixtures';
import { createTrip, invite, makeEditor } from './helpers';

/**
 * What survives a reload, a cleared cookie and a sign-out. The journeys the
 * reader was in the middle of should not quietly forget where they were.
 */
test.describe('the session', () => {
  test('SES-01 a reload keeps the travellers and the role', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Reload check');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Travellers' }).waitFor();

    await page.reload();

    await expect(page.getByRole('heading', { name: 'Travellers' })).toBeVisible();
    await expect(page.locator('.member-row', { hasText: ACCOUNTS.editor.email })).toBeVisible();
    await expect(
      page.locator('.member-row', { hasText: ACCOUNTS.owner.email }).locator('app-status-badge', {
        hasText: 'Owner',
      }),
    ).toBeVisible();
  });

  test('SES-02 a reader whose session ended is asked to sign in and come back', async ({
    ownerContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Session check');

    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);
    await page.getByRole('heading', { name: 'Travellers' }).waitFor();

    await ownerContext.clearCookies();
    await page.reload();

    await expect(page.getByRole('heading', { name: 'Sign in to carry on' })).toBeVisible();

    const link = page.locator('app-sign-in-prompt a.button');
    await expect(link).toHaveAttribute('href', new RegExp(`returnUrl=%2Ftrips%2F${trip.id}`));
  });

  test('SES-03 a signed-out reader asks the API for nothing', async ({ browser }) => {
    const context = await browser.newContext();
    const requested: string[] = [];
    context.on('request', (request) => requested.push(request.url()));

    const page = await context.newPage();
    await page.goto('/trips');

    await expect(page.getByRole('heading', { name: 'Sign in to carry on' })).toBeVisible();

    // The session probe runs; the private list never does.
    expect(requested.some((url) => url.includes('/api/trips'))).toBe(false);

    await context.close();
  });

  test('SES-04 an invitation still waits after a sign out', async ({
    browser,
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Sign out check');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.editor.email);

    const page = await editorContext.newPage();
    await page.goto('/trips');
    await page.getByRole('button', { name: 'Sign out' }).click();

    // The header stops claiming the account.
    await expect(page.getByRole('button', { name: 'Sign out' })).toHaveCount(0);

    const invitee = await browser.newContext();
    const landing = await invitee.newPage();
    await landing.goto(invitation.invitationUrl!);

    await expect(landing.getByRole('heading', { name: 'Sign in to carry on' })).toBeVisible();

    const link = landing.locator('app-sign-in-prompt a.button');
    await expect(link).toHaveAttribute('href', /returnUrl=%2Finvite%23token%3D/);

    await invitee.close();
  });

  test('SES-05 the travellers panel stays usable on a narrow screen', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Narrow screen check');
    await makeEditor(ownerContext, editorContext, trip.id);

    const page = await ownerContext.newPage();
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/trips/${trip.id}`);

    await page.getByRole('heading', { name: 'Invite a traveller' }).waitFor();

    // The rows stack, and every control stays reachable.
    await expect(page.locator('.member-row', { hasText: ACCOUNTS.editor.email })).toBeVisible();
    await expect(page.getByLabel('Email address')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Send invitation' })).toBeVisible();
  });
});
