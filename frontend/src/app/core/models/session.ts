/** The signed-in account, as the API describes it. */
export interface SessionUser {
  id: string;
  email: string;
}

/**
 * Whether the request carried a session.
 *
 * The API answers 200 either way, so asking on every page load is not a
 * failed request.
 */
export interface Session {
  authenticated: boolean;
  user: SessionUser | null;
}

/** What the reader types to sign in, and what registration stores. */
export interface Credentials {
  email: string;
  password: string;
}

/**
 * The token to echo in `X-XSRF-TOKEN`.
 *
 * Identity embeds the account in the token, so it is discarded whenever the
 * session changes.
 */
export interface CsrfToken {
  token: string;
}
