import { test, expect } from './fixtures';
import { INVITATION_ID, INVITATION_LINK, mockNewAccount } from './invitation-mocks';

const PASSWORD = 'Un-mot-de-passe-1';

test('a new user chooses a password and joins the organization', async ({ page }) => {
  const sent = await mockNewAccount(page);

  await page.goto(INVITATION_LINK);
  await page.getByLabel('Mot de passe').fill(PASSWORD);
  await page.getByRole('button', { name: /créer mon compte/i }).click();

  await expect(page.getByText(/votre compte est créé/i)).toBeVisible();
  await expect(page.getByRole('link', { name: /se connecter/i })).toBeVisible();
  expect(sent).toEqual([
    { invitationId: INVITATION_ID, token: 'raw-token', password: null },
    { invitationId: INVITATION_ID, token: 'raw-token', password: PASSWORD },
  ]);
});

test('an invalid or expired invitation shows a neutral message', async ({ page }) => {
  await page.route('**/api/v1/invitations/accept', (route) =>
    route.fulfill({
      status: 400,
      contentType: 'application/problem+json',
      body: JSON.stringify({ status: 400, code: 'Invitation.Invalid' }),
    }),
  );

  await page.goto(INVITATION_LINK);

  await expect(page.getByText(/cette invitation est invalide ou a expiré/i)).toBeVisible();
  await expect(page.getByLabel('Mot de passe')).toHaveCount(0);
});
