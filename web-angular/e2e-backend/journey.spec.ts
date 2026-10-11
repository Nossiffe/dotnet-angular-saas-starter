import { test, expect } from './fixtures';
import { linkFromEmail } from './mailpit';

// Unique per run: a local database keeps the accounts of earlier runs.
const run = Date.now();
const OWNER = `owner-${run}@saas.local`;
const ADMIN = `admin-${run}@saas.local`;
const PASSWORD = 'Journey-Password-1!';

/**
 * The whole product against the real API, its PostgreSQL database and its e-mails in Mailpit:
 * nothing is mocked. axe checks every page the journey goes through.
 */
test('an owner signs up, runs their organization, then removes the admin they invited', async ({
  page,
  browser,
  request,
  journey,
}, testInfo) => {
  journey.watch(page);

  await journey.step('the owner registers', async () => {
    await page.goto('/register');
    await expect(page.getByRole('heading', { name: 'Inscription' })).toBeVisible();
    await journey.expectAccessible(page, 'the registration page');
    await page.getByLabel('Email').fill(OWNER);
    await page.getByLabel('Mot de passe').fill(PASSWORD);
    await page.getByRole('button', { name: 'Créer le compte' }).click();
    await expect(page.getByRole('heading', { name: 'Connexion' })).toBeVisible();
  });

  await journey.step('the owner confirms their address from the e-mail', async () => {
    await page.goto(await linkFromEmail(request, OWNER, '/confirm-email'));
    await expect(page.getByText('Votre adresse email a été confirmée avec succès.')).toBeVisible();
    await journey.expectAccessible(page, 'the confirmation page');
    await page.getByRole('link', { name: 'Se connecter' }).click();
  });

  await journey.step('the owner signs in', async () => {
    await expect(page.getByRole('heading', { name: 'Connexion' })).toBeVisible();
    await journey.expectAccessible(page, 'the login page');
    await page.getByLabel('Email').fill(OWNER);
    await page.getByLabel('Mot de passe').fill(PASSWORD);
    await page.getByRole('button', { name: 'Se connecter' }).click();
    await expect(page.getByRole('heading', { name: 'Produits' })).toBeVisible();
    await expect(page.getByText('0 produit(s) au total')).toBeVisible();
    await journey.expectAccessible(page, 'the products page, empty');
  });

  await journey.step('the owner adds, edits and deletes a product', async () => {
    await page.getByLabel('Nom').fill('Stylo');
    await page.getByLabel('Prix').fill('2.50');
    await page.getByRole('button', { name: 'Ajouter' }).click();
    await expect(page.getByRole('row').filter({ hasText: 'Stylo' })).toContainText('€2.50');
    await journey.expectAccessible(page, 'the products page with a product');

    await page.getByRole('button', { name: 'Modifier Stylo' }).click();
    // The edit mode is rendered before the fields are filled.
    await expect(page.getByRole('form', { name: 'Modification de « Stylo »' })).toBeVisible();
    await expect(page.getByLabel('Nom')).toHaveValue('Stylo');
    await page.getByLabel('Nom').fill('Stylo bleu');
    await page.getByLabel('Prix').fill('3.20');
    await journey.expectAccessible(page, 'a product being edited');
    await page.getByRole('button', { name: 'Enregistrer' }).click();
    await expect(page.getByRole('status')).toHaveText('« Stylo bleu » est enregistré.');
    // The list shown is the one read again from the API after the change.
    await expect(page.getByRole('row').filter({ hasText: 'Stylo bleu' })).toContainText('€3.20');

    await page.getByRole('button', { name: 'Supprimer Stylo bleu' }).click();
    await expect(page.getByText('0 produit(s) au total')).toBeVisible();
    await expect(page.getByRole('row').filter({ hasText: 'Stylo' })).toHaveCount(0);
  });

  await journey.step('the owner invites an admin', async () => {
    await page.getByRole('link', { name: 'Organisation' }).click();
    await expect(page.getByRole('heading', { name: 'Organisation', level: 1 })).toBeVisible();
    await page.getByLabel('E-mail', { exact: true }).fill(ADMIN);
    await page.getByLabel('Rôle', { exact: true }).selectOption('Admin');
    await page.getByRole('button', { name: 'Inviter' }).click();
    await expect(page.getByRole('status')).toHaveText(`Invitation envoyée à ${ADMIN}.`);
    const invitations = page.getByRole('table', { name: 'Invitations en attente' });
    await expect(invitations.getByRole('row').filter({ hasText: ADMIN })).toContainText('Admin');
    await journey.expectAccessible(page, 'the Organization page with a pending invitation');
  });

  // The admin uses their own browser: no session or cookie shared with the owner.
  const adminContext = await browser.newContext({ baseURL: testInfo.project.use.baseURL! });
  const admin = await adminContext.newPage();
  journey.watch(admin);

  await journey.step('the admin accepts the invitation from the e-mail', async () => {
    await admin.goto(await linkFromEmail(request, ADMIN, '/accept-invitation'));
    await expect(admin.getByLabel('Mot de passe')).toBeVisible();
    await journey.expectAccessible(admin, 'the password step of the invitation');
    await admin.getByLabel('Mot de passe').fill(PASSWORD);
    await admin.getByRole('button', { name: 'Créer mon compte et rejoindre' }).click();
    await expect(
      admin.getByText("Votre compte est créé et vous avez rejoint l'organisation."),
    ).toBeVisible();
    await journey.expectAccessible(admin, 'the accepted invitation');
    await admin.getByRole('link', { name: 'Se connecter' }).click();
  });

  await journey.step('the admin signs in', async () => {
    await expect(admin.getByRole('heading', { name: 'Connexion' })).toBeVisible();
    await admin.getByLabel('Email').fill(ADMIN);
    await admin.getByLabel('Mot de passe').fill(PASSWORD);
    await admin.getByRole('button', { name: 'Se connecter' }).click();
    await expect(admin.getByRole('heading', { name: 'Produits' })).toBeVisible();
    // Admin of the owner's organization: the Organization screen is open to them.
    await expect(admin.getByRole('link', { name: 'Organisation' })).toBeVisible();
    await journey.expectAccessible(admin, "the admin's products page");
  });

  await journey.step('the owner sees the admin among the members', async () => {
    // Back to the screen through the navigation: the lists are read again.
    await page.getByRole('link', { name: 'Produits' }).click();
    await page.getByRole('link', { name: 'Organisation' }).click();
    const members = page.getByRole('table', { name: "Membres de l'organisation" });
    await expect(members.getByRole('row').filter({ hasText: ADMIN })).toBeVisible();
    await expect(page.getByRole('combobox', { name: `Rôle de ${ADMIN}` })).toHaveValue('Admin');
    // The invitation was accepted: it is no longer pending.
    await expect(page.getByText('Aucune invitation en attente.')).toBeVisible();
    await journey.expectAccessible(page, 'the Organization page with the admin');
  });

  await journey.step('the owner removes the admin', async () => {
    await page.getByRole('button', { name: `Retirer ${ADMIN}` }).click();
    const dialog = page.getByRole('dialog', { name: `Retirer ${ADMIN} ?` });
    await expect(dialog).toBeVisible();
    // axe reads the colours: it waits for the end of the dialog's opening animation.
    await expect(page.locator('mat-dialog-container')).not.toHaveClass(/mdc-dialog--opening/);
    await journey.expectAccessible(page, 'the removal confirmation');
    await dialog.getByRole('button', { name: 'Retirer' }).click();
    await expect(page.getByRole('status')).toHaveText(`${ADMIN} a été retiré de l'organisation.`);
    await expect(page.getByRole('row').filter({ hasText: ADMIN })).toHaveCount(0);
  });

  await journey.step('the removed admin loses access', async () => {
    // The admin's tab still holds an access token valid for 15 minutes. The API re-reads the
    // membership on every request: the token is refused (401 Auth.SessionExpired), the refresh
    // that follows is refused too, and the page sends the admin back to the login page.
    const members = admin.waitForResponse(
      (r) => r.request().method() === 'GET' && new URL(r.url()).pathname.endsWith('/members'),
    );
    const refresh = admin.waitForResponse((r) => r.url().endsWith('/api/v1/auth/refresh'));
    // Awaited below; if an assertion fails first, the waiter left pending adds no second error.
    for (const waiter of [members, refresh]) waiter.catch(() => undefined);
    await admin.getByRole('link', { name: 'Organisation' }).click();

    const refused = await members;
    expect(refused.status()).toBe(401);
    expect(((await refused.json()) as { code?: string }).code).toBe('Auth.SessionExpired');
    expect((await refresh).status()).toBe(401);
    await expect(admin).toHaveURL(/\/login\?returnUrl=%2Forganization$/);
    await expect(admin.getByText('Votre session a expiré. Reconnectez-vous.')).toBeVisible();
    await expect(admin.getByRole('link', { name: 'Organisation' })).toHaveCount(0);
    await journey.expectAccessible(admin, 'the login page after the removal');
  });

  await adminContext.close();
});
