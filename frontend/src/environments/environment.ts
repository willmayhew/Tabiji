/**
 * The Angular dev server proxies `/api` and `/health` to the backend (see
 * `proxy.conf.json`), so the browser only ever talks to its own origin.
 * Point this at an absolute URL to call a hosted API directly.
 */
export const environment = {
  production: false,
  apiBaseUrl: '',
};
