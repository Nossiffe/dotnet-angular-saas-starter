import { test as base } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

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
  makeAxeBuilder: async ({ page }, use) => {
    await use(() =>
      new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']),
    );
  },
});
export { expect } from '@playwright/test';
