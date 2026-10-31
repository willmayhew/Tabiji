import { ACCOUNTS, api, expect, test } from './fixtures';
import { createTrip, invite } from './helpers';

/**
 * The journey an invitation makes: an owner issues one from the Travellers
 * panel, the addressed account opens the link, signs in if it has to, and
 * decides. Every test issues its own invitation, because a token is single
 * use and is never handed out twice.
 */
test.describe('the invitation journey', () => {
  test('INV-01 an owner invites an address from the travellers panel', async ({ ownerContext }) => {
    const trip = await createTrip(ownerContext, 'Kyoto in spring');
    const page = await ownerContext.newPage();
    await page.goto(`/trips/${trip.id}`);

    await page.getByRole('heading', { name: 'Invite a traveller' }).waitFor();

    await page.getByLabel('Email address').fill(ACCOUNTS.editor.email);
    await page.getByRole('button', { name: 'Send invitation' }).click();

    await expect(page.getByText(`Invitation sent to ${ACCOUNTS.editor.email}.`)).toBeVisible();

    // Development has no email provider, so the link is shown for the owner to share.
    const link = page.getByRole('link', { name: 'Open the invitation link' });
    await expect(link).toHaveAttribute('href', /\/invite#token=/);

    // The invitation joins the pending list with its expiry.
    await expect(page.getByRole('heading', { name: 'Pending invitations' })).toBeVisible();
    await expect(page.locator('.invitation-row', { hasText: ACCOUNTS.editor.email })).toBeVisible();
    await expect(page.locator('.invitation-row__expiry')).toContainText('Expires');
  });

  test('INV-02 an invitee signs in from the link and accepts', async ({
    browser,
    ownerContext,
    editorContext,
  }) => {
    // editorContext is what registers the addressed account; the invitee below
    // still arrives with no session, the way a real link arrival does.
    const trip = await createTrip(ownerContext, 'Osaka food run');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.editor.email);

    const invitee = await browser.newContext();
    const page = await invitee.newPage();
    await page.goto(invitation.invitationUrl!);

    await expect(page.getByRole('heading', { name: 'Sign in to carry on' })).toBeVisible();

    // The sign-in link carries the invitation back in, token fragment and all.
    const signIn = page.locator('app-sign-in-prompt').getByRole('link', { name: 'Sign in' });
    await expect(signIn).toHaveAttribute('href', /returnUrl=%2Finvite%23token%3D/);
    await signIn.click();

    await page.getByLabel('Email').fill(ACCOUNTS.editor.email);
    await page.getByLabel('Password').fill(ACCOUNTS.editor.password);
    await page.getByRole('button', { name: 'Sign in' }).click();

    // Back on the invitation, which the API previews for this address only.
    await expect(page.getByRole('heading', { name: 'Osaka food run' })).toBeVisible();
    await expect(page.getByText('invited you to join as an editor')).toBeVisible();
    await expect(page.getByText('Expires')).toBeVisible();

    await page.getByRole('button', { name: 'Accept invitation' }).click();

    await expect(page).toHaveURL(new RegExp(`/trips/${trip.id}`));
    await expect(page.getByRole('heading', { name: 'Travellers' })).toBeVisible();
    await expect(page.locator('.member-row', { hasText: ACCOUNTS.editor.email })).toBeVisible();

    // An editor sees the people, but none of the owner's controls.
    await expect(page.getByRole('heading', { name: 'Invite a traveller' })).toHaveCount(0);
    await invitee.close();
  });

  test('INV-03 a reader with no account can register and still accept', async ({
    browser,
    ownerContext,
  }) => {
    const email = 'newcomer@tabiji.test';
    const trip = await createTrip(ownerContext, 'Hiroshima peace park');
    const invitation = await invite(ownerContext, trip.id, email);

    const invitee = await browser.newContext();
    const page = await invitee.newPage();
    await page.goto(invitation.invitationUrl!);
    await expect(page.getByRole('heading', { name: 'Sign in to carry on' })).toBeVisible();

    // The prompt offers an account, and the invitation survives the detour.
    await page.getByRole('link', { name: 'Create an account' }).click();
    await expect(page).toHaveURL(/\/register/);

    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill('CorrectHorse42!');
    await page.getByLabel('Repeat the password').fill('CorrectHorse42!');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();

    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password').fill('CorrectHorse42!');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByRole('heading', { name: 'Hiroshima peace park' })).toBeVisible();
    await page.getByRole('button', { name: 'Accept invitation' }).click();

    await expect(page).toHaveURL(new RegExp(`/trips/${trip.id}`));
    await expect(page.locator('.member-row', { hasText: email })).toBeVisible();
    await invitee.close();
  });

  test('INV-04 an invitee can decline', async ({ ownerContext, editorContext }) => {
    const trip = await createTrip(ownerContext, 'Nara day trip');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.editor.email);

    const page = await editorContext.newPage();
    await page.goto(invitation.invitationUrl!);
    await expect(page.getByRole('heading', { name: 'Nara day trip' })).toBeVisible();

    await page.getByRole('button', { name: 'Decline' }).click();
    await expect(page.getByRole('heading', { name: 'Invitation declined' })).toBeVisible();

    // The owner sees the decision on the trip.
    const ownerPage = await ownerContext.newPage();
    await ownerPage.goto(`/trips/${trip.id}`);
    await expect(
      ownerPage.locator('.invitation-row', { hasText: ACCOUNTS.editor.email }),
    ).toContainText('Declined');
  });

  test('INV-05 an accepted invitation says so when the link is opened again', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Kobe beef itinerary');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.editor.email);

    const page = await editorContext.newPage();
    await page.goto(invitation.invitationUrl!);
    await page.getByRole('button', { name: 'Accept invitation' }).click();
    await expect(page).toHaveURL(new RegExp(`/trips/${trip.id}`));

    // The token is single use: the same link now reports the decision.
    await page.goto(invitation.invitationUrl!);
    await expect(page.locator('app-status-badge', { hasText: 'Accepted' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Open the trip' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Accept invitation' })).toHaveCount(0);
  });

  test('INV-06 an unknown token is refused without guessing', async ({ editorContext }) => {
    const page = await editorContext.newPage();
    await page.goto('/invite#token=not-a-real-token');

    await expect(
      page.getByRole('heading', { name: 'This invitation is not for this account' }),
    ).toBeVisible();
    await expect(page.getByRole('button', { name: 'Accept invitation' })).toHaveCount(0);
  });

  test('INV-07 a revoked invitation stops an acceptance', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Hakone weekend');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.editor.email);

    const page = await editorContext.newPage();
    await page.goto(invitation.invitationUrl!);
    await expect(page.getByRole('button', { name: 'Accept invitation' })).toBeVisible();

    // The owner takes the invitation back while the invitee is on the page.
    const revoked = await api(
      ownerContext,
      'DELETE',
      `/api/trips/${trip.id}/invitations/${invitation.id}`,
    );
    expect(revoked.status()).toBe(204);

    await page.getByRole('button', { name: 'Accept invitation' }).click();
    await expect(page.getByRole('alert')).toContainText('revoked');
  });

  test('INV-08 a link only works for the address it names', async ({
    ownerContext,
    strangerContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Kanazawa museums');
    const invitation = await invite(ownerContext, trip.id, ACCOUNTS.editor.email);

    const page = await strangerContext.newPage();
    await page.goto(invitation.invitationUrl!);

    await expect(
      page.getByRole('heading', { name: 'This invitation is not for this account' }),
    ).toBeVisible();
    // The trip's name is not leaked to the wrong account.
    await expect(page.getByText('Kanazawa museums')).toHaveCount(0);
  });

  test('INV-09 an invitation is accepted from the trips page', async ({
    ownerContext,
    editorContext,
  }) => {
    const trip = await createTrip(ownerContext, 'Tottori sand dunes');
    await invite(ownerContext, trip.id, ACCOUNTS.editor.email);

    const page = await editorContext.newPage();
    await page.goto('/trips');

    await expect(page.getByRole('heading', { name: 'Invitations waiting for you' })).toBeVisible();
    const row = page.locator('.invitation-row', { hasText: 'Tottori sand dunes' });
    await row.getByRole('button', { name: 'Accept' }).click();

    await expect(page).toHaveURL(new RegExp(`/trips/${trip.id}`));
    await expect(page.getByRole('heading', { name: 'Tottori sand dunes' })).toBeVisible();
  });
});
