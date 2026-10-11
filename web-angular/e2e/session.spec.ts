import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { mockProducts } from './products-mocks';
import { sessionCookieApi } from './session-mocks';

// A call waiting for the lock another tab holds (Web Locks are shared by the origin's tabs).
const waitingForLock = (tab: Page) =>
  tab.evaluate(async () => ((await navigator.locks.query()).pending ?? []).length > 0);

// The tabs of a browser share the refresh cookie, and the API rotates it on every refresh. A call
// that presents the cookie while another tab's refresh is on its way presents the token that
// refresh has just replaced.
async function twoTabs(page: Page) {
  const session = await sessionCookieApi(page.context());
  const second = await page.context().newPage();
  for (const tab of [page, second]) {
    await mockProducts(tab);
    await session.serve(tab);
  }
  return { session, second };
}

// The second refresh would be taken for a theft, and the API would revoke the session.
test('two tabs that refresh the session at the same time both stay signed in', async ({ page }) => {
  const { session, second } = await twoTabs(page);

  // Both tabs open while the answer to the first one's refresh is on its way. The answer is held
  // until the second tab has started its own refresh: sent to the API, or waiting for the lock.
  const release = session.holdNext();
  await page.goto('/products');
  await expect.poll(() => session.state.received).toEqual(['rt-0']);
  await second.goto('/products');
  await expect
    .poll(async () => session.state.received.length === 2 || (await waitingForLock(second)))
    .toBe(true);
  release();

  for (const [index, tab] of [page, second].entries()) {
    await expect(
      tab.getByRole('heading', { name: 'Produits' }),
      `tab ${index + 1} still signed in`,
    ).toBeVisible();
  }
  // The second tab presented the token the first one received, not the one it replaced.
  expect(session.state.received).toEqual(['rt-0', 'rt-1']);
  expect(session.state.reused).toEqual([]);

  // The cookie left in the browser is still valid: a reload refreshes the session again.
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Produits' })).toBeVisible();
  expect(session.state.received).toEqual(['rt-0', 'rt-1', 'rt-2']);
});

// The logout would revoke nothing, and the refresh's answer would set a valid cookie again.
test('a logout sent while another tab refreshes the session closes it', async ({ page }) => {
  const { session, second } = await twoTabs(page);
  await page.goto('/products');
  await expect(page.getByRole('heading', { name: 'Produits' })).toBeVisible();

  // The second tab's refresh has rotated the token; its answer, with the new cookie, is held until
  // the logout has started: sent to the API, or waiting for the lock.
  const release = session.holdNext();
  await second.goto('/products');
  await expect.poll(() => session.state.received).toEqual(['rt-0', 'rt-1']);
  await page.getByRole('button', { name: 'Déconnexion' }).click();
  await expect
    .poll(async () => session.state.loggedOut.length === 1 || (await waitingForLock(page)))
    .toBe(true);
  release();

  await expect(page.getByRole('heading', { name: 'Connexion' })).toBeVisible();
  // The logout presented the cookie the refresh set, and closed that session.
  expect(session.state.loggedOut).toEqual(['rt-2']);
  expect(session.state.current).toBeNull();
});
