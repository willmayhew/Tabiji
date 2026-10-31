import { ACCOUNTS, api, expect, type BrowserContext } from './fixtures';

/** A date n days from today, as the API's DateOnly binding accepts it. */
export function iso(daysFromNow: number): string {
  return new Date(Date.now() + daysFromNow * 86_400_000).toISOString().slice(0, 10);
}

export interface CreatedTrip {
  id: string;
  name: string;
}

/** Creates a trip on the API, so a test can get on with the journey under test. */
export async function createTrip(context: BrowserContext, name: string): Promise<CreatedTrip> {
  const startsOn = iso(7);
  const endsOn = iso(14);

  const response = await api(context, 'POST', '/api/trips', {
    name,
    startsOn,
    endsOn,
    destinations: [{ name: 'Tokyo', startsOn, endsOn }],
  });

  expect(response.status(), await response.text()).toBe(201);
  return (await response.json()) as CreatedTrip;
}

export interface CreatedInvitation {
  id: string;
  email: string;
  status: string;
  expiresAt: string;
  invitationUrl: string | null;
}

/**
 * Issues an invitation on the API. The suite runs the API in Development with
 * no email provider, so the response carries the link the owner would share.
 */
export async function invite(
  context: BrowserContext,
  tripId: string,
  email: string,
): Promise<CreatedInvitation> {
  const response = await api(context, 'POST', `/api/trips/${tripId}/invitations`, { email });

  expect(response.status(), await response.text()).toBe(201);

  const invitation = (await response.json()) as CreatedInvitation;
  expect(invitation.invitationUrl, 'the development link must be present').toBeTruthy();

  return invitation;
}

/** The raw token from a development invitation link. */
export function tokenOf(invitationUrl: string): string {
  return new URLSearchParams(new URL(invitationUrl).hash.slice(1)).get('token') ?? '';
}

export interface TripMembers {
  viewerRole: string;
  members: Array<{ userId: string; email: string; role: string }>;
}

/** The members of a trip, including the caller's own role. */
export async function membersOf(context: BrowserContext, tripId: string): Promise<TripMembers> {
  const response = await api(context, 'GET', `/api/trips/${tripId}/members`);
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as TripMembers;
}

/**
 * Makes the editor a member of a trip, the way an accepted invitation does,
 * so a test about membership management does not have to walk the whole
 * invitation journey to get there.
 */
export async function makeEditor(
  ownerContext: BrowserContext,
  editorContext: BrowserContext,
  tripId: string,
): Promise<void> {
  const invitation = await invite(ownerContext, tripId, ACCOUNTS.editor.email);

  const accepted = await api(editorContext, 'POST', '/api/invitations/accept', {
    token: tokenOf(invitation.invitationUrl!),
  });

  expect(accepted.status()).toBe(200);
}
