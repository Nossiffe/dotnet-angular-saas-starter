import { defineConfig } from '@playwright/test';

/**
 * The journey against the real backend (e2e-backend/). PostgreSQL, Mailpit and the API must
 * already run: `docker compose --profile full up -d --build db mailpit api` (CONTRIBUTING.md).
 * Playwright starts the front, whose dev server proxies /api to the API on port 5123.
 */
export default defineConfig({
  testDir: './e2e-backend',
  // A single journey, in order. No retry: a second run would register again and add its calls
  // to the API's auth budget (see e2e-backend/fixtures.ts).
  workers: 1,
  retries: 0,
  timeout: 3 * 60_000,
  outputDir: 'test-results/e2e-backend',
  reporter: process.env['CI']
    ? [['list'], ['html', { open: 'never', outputFolder: 'playwright-report/e2e-backend' }]]
    : 'list',
  use: {
    baseURL: 'http://localhost:4200',
    // Kept only when the journey fails: the trace (every step, network call and DOM snapshot)
    // and a screenshot of the page where it stopped.
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: 'npm start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env['CI'],
    timeout: 120_000,
  },
});
