import { defineConfig, devices } from '@playwright/test';

/**
 * Browser verification of the collaboration journeys.
 *
 * The suite runs against a throwaway pair of servers on ports that never
 * collide with a development session (4300 for the app, 5081 for the API),
 * and against its own LocalDB database, TabijiE2E, which global-setup drops
 * and recreates from the real migrations before every run. The development
 * database is never opened.
 *
 * Everything is serial on purpose: one API, one database, and fixtures that
 * create their own trips. Chromium only — the product supports modern
 * browsers, not legacy ones.
 */
const applicationUrl = 'http://localhost:4300';
const apiUrl = 'http://localhost:5081';
const database = 'TabijiE2E';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 30_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  globalSetup: './e2e/global-setup.ts',
  use: {
    baseURL: applicationUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      command: 'dotnet run --project ../backend/Tabiji.Api --no-launch-profile',
      url: `${apiUrl}/health`,
      reuseExistingServer: false,
      timeout: 180_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: apiUrl,
        // A database of its own, so a run never touches the development data.
        ConnectionStrings__Tabiji: `Server=(localdb)\\MSSQLLocalDB;Database=${database};Trusted_Connection=True;TrustServerCertificate=True`,
        // The development invitation link has to point at this suite's app.
        Invitations__PublicBaseUrl: applicationUrl,
      },
    },
    {
      command: 'npm start -- --port 4300 --proxy-config proxy.conf.e2e.json',
      url: applicationUrl,
      reuseExistingServer: false,
      timeout: 180_000,
    },
  ],
});
