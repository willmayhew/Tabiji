import {
  expect,
  test as base,
  type APIRequestContext,
  type BrowserContext,
} from '@playwright/test';

/**
 * The accounts the suite signs in as. They live only in the suite's own
 * database, TabijiE2E, which is dropped and recreated before every run.
 *
 * Each account is registered and signed in once per run through the real
 * endpoints, and the resulting cookie jar is reused for every test. That is
 * not a shortcut: /api/auth/login is rate limited to ten requests a minute,
 * so a suite that signed in per test would throttle itself.
 */
export const ACCOUNTS = {
  owner: { email: 'owner@tabiji.test', password: 'CorrectHorse42!' },
  editor: { email: 'editor@tabiji.test', password: 'CorrectHorse42!' },
  stranger: { email: 'stranger@tabiji.test', password: 'CorrectHorse42!' },
} as const;

export type AccountRole = keyof typeof ACCOUNTS;

type StorageState = Awaited<ReturnType<APIRequestContext['storageState']>>;

const storageStates = new Map<AccountRole, StorageState>();

async function accountState(request: APIRequestContext, role: AccountRole): Promise<StorageState> {
  const cached = storageStates.get(role);
  if (cached !== undefined) {
    return cached;
  }

  const { email, password } = ACCOUNTS[role];

  const registered = await request.post('/api/auth/register', { data: { email, password } });
  if (registered.status() !== 200 && registered.status() !== 400) {
    throw new Error(
      `Registering ${email} answered ${registered.status()}: ${await registered.text()}`,
    );
  }

  const loggedIn = await request.post('/api/auth/login?useCookies=true', {
    data: { email, password },
  });
  expect(loggedIn.status(), await loggedIn.text()).toBe(200);

  const state = await request.storageState();
  storageStates.set(role, state);

  return state;
}

type CollaborationFixtures = {
  ownerContext: BrowserContext;
  editorContext: BrowserContext;
  strangerContext: BrowserContext;
};

/**
 * One browser context per account, so a test can hold an owner and an invitee
 * at the same time with genuinely separate cookies. Contexts are closed when
 * the test ends.
 */
export const test = base.extend<CollaborationFixtures>({
  ownerContext: async ({ browser, request }, use) => {
    const context = await browser.newContext({
      storageState: await accountState(request, 'owner'),
    });
    await use(context);
    await context.close();
  },
  editorContext: async ({ browser, request }, use) => {
    const context = await browser.newContext({
      storageState: await accountState(request, 'editor'),
    });
    await use(context);
    await context.close();
  },
  strangerContext: async ({ browser, request }, use) => {
    const context = await browser.newContext({
      storageState: await accountState(request, 'stranger'),
    });
    await use(context);
    await context.close();
  },
});

export { expect };

/**
 * Reads the antiforgery token the way the Angular client does, so a test can
 * talk to the API directly and prove what the screen refuses to offer.
 */
export async function csrfToken(context: BrowserContext): Promise<string> {
  const response = await context.request.get('/api/auth/csrf');
  expect(response.status()).toBe(200);
  return ((await response.json()) as { token: string }).token;
}

/** A state-changing call on the API, with the antiforgery header in place. */
export async function api(
  context: BrowserContext,
  method: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE',
  path: string,
  data?: unknown,
) {
  const token = await csrfToken(context);
  return context.request.fetch(path, {
    method,
    data,
    headers: { 'X-XSRF-TOKEN': token },
  });
}
