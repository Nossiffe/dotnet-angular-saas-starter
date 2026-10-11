import { test as base } from '@playwright/test';
import type AxeBuilder from '@axe-core/playwright';
import { axeBuilder } from './axe';

// These specs run without a backend. The API answers every call with 401, as it
// does for a visitor without a session: without that, the startup session
// refresh would hit a closed port and show a network error snackbar.
export const test = base.extend<{ anonymousVisitor: void; makeAxeBuilder: () => AxeBuilder }>({
  anonymousVisitor: [
    async ({ page }, use) => {
      await page.route('**/api/**', (route) =>
        route.fulfill({
          status: 401,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Unauthorized', status: 401 }),
        }),
      );
      await use();
    },
    { auto: true },
  ],
  // WCAG 2.2 A and AA, once the open snack-bars have been announced (see e2e/axe.ts).
  makeAxeBuilder: async ({ page }, use) => {
    await use(() => axeBuilder(page));
  },
});
export { expect } from '@playwright/test';
