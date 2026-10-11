import { test as base, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/**
 * The API's auth policy: 10 requests per 30 seconds and per IP, on every /api/v1/auth route
 * (src/Api/Hosting/RateLimitingExtensions.cs). Both users of the journey come from the same IP:
 * the journey stays within this budget over its whole length, so the rate limiter, active as in
 * production, can never refuse one of its calls, whatever the timing.
 */
const AUTH_PERMIT_LIMIT = 10;

interface Journey {
  /** Watches a page: its calls to /api/v1/auth are counted, a 429 is recorded. */
  watch(page: Page): void;
  /**
   * A step of the journey, named in the report and the trace. It ends by checking the rate limits:
   * if it fails because of a 429, that is the error reported, rather than the timeout it caused.
   */
  step(title: string, body: () => Promise<void>): Promise<void>;
  /** Fails on any WCAG 2.2 A or AA violation axe finds on the page as it is. */
  expectAccessible(page: Page, state: string): Promise<void>;
}

export const test = base.extend<{ journey: Journey }>({
  journey: async ({}, use) => {
    const authCalls: string[] = [];
    const tooManyRequests: string[] = [];
    const checkRateLimits = () => {
      expect(tooManyRequests, 'calls refused by the rate limiter (429)').toEqual([]);
      expect(
        authCalls.length,
        `calls to /api/v1/auth (budget ${AUTH_PERMIT_LIMIT}): ${authCalls.join(', ')}`,
      ).toBeLessThanOrEqual(AUTH_PERMIT_LIMIT);
    };
    await use({
      watch(page) {
        page.on('request', (request) => {
          const { pathname } = new URL(request.url());
          if (pathname.startsWith('/api/v1/auth/')) authCalls.push(pathname);
        });
        page.on('response', (response) => {
          if (response.status() === 429) tooManyRequests.push(response.url());
        });
      },
      async step(title, body) {
        await base.step(title, async () => {
          try {
            await body();
          } catch (error) {
            checkRateLimits();
            throw error;
          }
          checkRateLimits();
        });
      },
      async expectAccessible(page, state) {
        const results = await new AxeBuilder({ page })
          .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
          .analyze();
        const violations = results.violations.map((v) => ({
          rule: v.id,
          targets: v.nodes.map((n) => n.target.join(' ')),
        }));
        expect(violations, `axe on ${state} (${page.url()})`).toEqual([]);
      },
    });
  },
});

export { expect };
