import type { BrowserContext, Page } from '@playwright/test';
import { OWNER_ID, accessToken } from './organization-mocks';

const COOKIE = 'rt';
const COOKIE_PATH = '/api/v1/auth';
const deleteCookie = `${COOKIE}=; Path=${COOKIE_PATH}; Max-Age=0`;

/**
 * The refresh and logout endpoints as the API runs them, shared by the tabs of a browser context.
 * The refresh token lives in the HttpOnly cookie `rt`, and every refresh rotates it. A token
 * already replaced by a rotation, presented again, is taken for a theft: the session is revoked,
 * the refresh refused and the cookie deleted (RefreshTokenStore.ValidateAndRotateAsync). A logout
 * revokes the token it presents, if it is the current one, and deletes the cookie.
 *
 * `holdNext()` holds the answer to the next refresh, the rotation being already done, as when the
 * API has committed it and its answer is still on its way.
 */
export async function sessionCookieApi(context: BrowserContext) {
  await context.addCookies([
    { name: COOKIE, value: 'rt-0', domain: 'localhost', path: COOKIE_PATH, httpOnly: true },
  ]);
  const state = {
    /** The valid token, or null once the session is revoked. */
    current: 'rt-0' as string | null,
    replaced: new Set<string>(),
    /** The token each refresh presented, in order. */
    received: [] as string[],
    /** The replaced tokens presented again. */
    reused: [] as string[],
    /** The token each logout presented. */
    loggedOut: [] as string[],
  };
  let held: Promise<void> | null = null;

  function holdNext(): () => void {
    let release!: () => void;
    held = new Promise<void>((resolve) => (release = resolve));
    return release;
  }

  async function serve(tab: Page): Promise<void> {
    await tab.route('**/api/v1/auth/refresh', async (route) => {
      const sent = cookie(await route.request().headerValue('cookie'));
      state.received.push(sent);

      if (sent !== state.current) {
        if (state.replaced.has(sent)) {
          state.reused.push(sent);
          state.current = null;
        }
        return route.fulfill({
          status: 401,
          headers: { 'set-cookie': deleteCookie },
          contentType: 'application/problem+json',
          body: JSON.stringify({ status: 401, code: 'Auth.SessionExpired' }),
        });
      }

      const next = `rt-${state.received.length}`;
      state.replaced.add(sent);
      state.current = next;
      const gate = held;
      held = null;
      if (gate) await gate;
      return route.fulfill({
        headers: { 'set-cookie': `${COOKIE}=${next}; Path=${COOKIE_PATH}; HttpOnly` },
        json: { accessToken: accessToken(OWNER_ID, 'Owner'), expiresAt: '2030-01-01T00:00:00Z' },
      });
    });

    await tab.route('**/api/v1/auth/logout', async (route) => {
      const sent = cookie(await route.request().headerValue('cookie'));
      state.loggedOut.push(sent);
      if (sent === state.current) state.current = null;
      return route.fulfill({ status: 204, headers: { 'set-cookie': deleteCookie } });
    });
  }

  return { state, serve, holdNext };
}

function cookie(header: string | null): string {
  const pair = header
    ?.split(';')
    .map((part) => part.trim())
    .find((part) => part.startsWith(`${COOKIE}=`));
  return pair?.slice(COOKIE.length + 1) ?? '';
}
