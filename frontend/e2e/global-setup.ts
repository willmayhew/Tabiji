import { execFileSync } from 'node:child_process';
import { join } from 'node:path';

/**
 * Prepares the suite's own database before any server starts.
 *
 * TabijiE2E is dropped and recreated from the real migrations, so a run begins
 * with a known schema and never reads or writes the development database. The
 * drop happens here rather than in teardown because the API still holds
 * connections once the tests have run.
 *
 * Playwright runs this file with the frontend directory as the working
 * directory, which is also where the servers in playwright.config.ts are
 * started from.
 */
const repoRoot = join(process.cwd(), '..');
const database = 'TabijiE2E';

const connectionString = `Server=(localdb)\\MSSQLLocalDB;Database=${database};Trusted_Connection=True;TrustServerCertificate=True`;

export default async function globalSetup(): Promise<void> {
  const environment = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    ConnectionStrings__Tabiji: connectionString,
  };

  const entityFramework = (args: string[]) => {
    try {
      execFileSync('dotnet', ['ef', ...args], {
        cwd: repoRoot,
        env: environment,
        stdio: 'inherit',
      });
    } catch (error) {
      throw new Error(
        `Could not prepare the ${database} database for the browser suite. ` +
          `Run "dotnet tool restore" once, and check that SQL Server LocalDB is installed. ` +
          `Underlying error: ${error}`,
      );
    }
  };

  const project = [
    '--project',
    'backend/Tabiji.Infrastructure',
    '--startup-project',
    'backend/Tabiji.Api',
  ];

  // A fresh run has nothing to drop; a failed drop is only fatal when the
  // update below also fails.
  try {
    entityFramework(['database', 'drop', '--force', ...project]);
  } catch {
    // Intentionally ignored: the database may not exist yet.
  }

  entityFramework(['database', 'update', ...project]);
}
