import type { Page } from '@playwright/test';

export const INVITATION_ID = '0199c9a0-0000-7000-8000-000000000001';
export const INVITATION_LINK = `/accept-invitation?invitationId=${INVITATION_ID}&token=raw-token`;

// The accept call answers like the API for an e-mail that has no account yet: without a password,
// 400 with the code Invitation.PasswordRequired; with a password, 200. Returns the bodies sent.
export async function mockNewAccount(page: Page): Promise<unknown[]> {
  const sent: unknown[] = [];
  await page.route('**/api/v1/invitations/accept', async (route) => {
    const body = route.request().postDataJSON() as { password: string | null };
    sent.push(body);
    await (body.password === null
      ? route.fulfill({
          status: 400,
          contentType: 'application/problem+json',
          body: JSON.stringify({ status: 400, code: 'Invitation.PasswordRequired' }),
        })
      : route.fulfill({ status: 200, json: { isNewUser: true } }));
  });
  return sent;
}
