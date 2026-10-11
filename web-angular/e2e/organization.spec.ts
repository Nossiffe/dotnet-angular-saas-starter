import { test, expect } from './fixtures';
import { ADMIN_ID, MEMBER_ID, mockOrganization } from './organization-mocks';

test('an owner opens the Organization screen from the navigation', async ({ page }) => {
  await mockOrganization(page, 'Owner');

  await page.goto('/products');
  await page.getByRole('link', { name: 'Organisation' }).click();

  await expect(page.getByRole('heading', { name: 'Organisation', level: 1 })).toBeVisible();
  const members = page.getByRole('table', { name: "Membres de l'organisation" });
  await expect(members.getByRole('row')).toHaveCount(4);
  await expect(members.getByRole('row', { name: /owner@saas\.local/ })).toContainText('Owner');
  const invitations = page.getByRole('table', { name: 'Invitations en attente' });
  await expect(invitations.getByRole('row', { name: /admin\.other@saas\.local/ })).toContainText(
    'admin@saas.local',
  );
});

test('an owner removes an admin and the invitation that admin sent', async ({ page }) => {
  const calls = await mockOrganization(page, 'Owner');
  await page.goto('/organization');

  await page.getByRole('button', { name: 'Retirer admin@saas.local' }).click();
  const dialog = page.getByRole('dialog', { name: 'Retirer admin@saas.local ?' });
  await expect(dialog.getByRole('listitem')).toHaveText(['admin.other@saas.local (Admin)']);
  const revokeToo = dialog.getByRole('checkbox', { name: 'Les révoquer aussi' });
  await expect(revokeToo).toBeChecked();
  // Le focus va à la case, pas au bouton destructif.
  await expect(revokeToo).toBeFocused();
  await dialog.getByRole('button', { name: 'Retirer' }).click();

  await expect(page.getByRole('status')).toHaveText(
    "admin@saas.local a été retiré de l'organisation. Invitations révoquées : 1.",
  );
  await expect(page.getByRole('heading', { name: 'Membres' })).toBeFocused();
  await expect(page.getByRole('cell', { name: 'admin@saas.local', exact: true })).toHaveCount(0);
  await expect(page.getByRole('cell', { name: 'admin.other@saas.local' })).toHaveCount(0);
  expect(calls.filter((c) => c.method === 'DELETE').map((c) => c.path)).toEqual([
    `/members/${ADMIN_ID}`,
    '/invitations/0199c9a0-0000-7000-8000-0000000000b2',
  ]);
});

// « Retirer » est désactivé pendant la relecture des invitations, et le navigateur lui retire le
// focus : la confirmation le lui rend en se fermant, à la souris comme au clavier.
test('a cancelled removal gives the focus back to its button', async ({ page }) => {
  await mockOrganization(page, 'Owner');
  await page.goto('/organization');
  const remove = page.getByRole('button', { name: 'Retirer admin@saas.local' });
  const dialog = page.getByRole('dialog', { name: 'Retirer admin@saas.local ?' });

  await remove.click();
  await dialog.getByRole('button', { name: 'Annuler' }).click();
  await expect(dialog).toHaveCount(0);
  await expect(remove).toBeFocused();

  await page.keyboard.press('Enter');
  await expect(dialog).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await expect(remove).toBeFocused();
});

test('an owner changes the role of a member with the Apply button', async ({ page }) => {
  const calls = await mockOrganization(page, 'Owner');
  await page.goto('/organization');

  await page.getByRole('combobox', { name: 'Rôle de member@saas.local' }).selectOption('Admin');
  expect(calls.filter((c) => c.method === 'PUT')).toEqual([]);
  await page.getByRole('button', { name: 'Appliquer le rôle de member@saas.local' }).click();

  await expect(page.getByRole('status')).toHaveText('Rôle de member@saas.local : Admin.');
  expect(calls).toContainEqual({
    method: 'PUT',
    path: `/members/${MEMBER_ID}/role`,
    body: { role: 'Admin' },
  });
  // La liste relue montre le rôle enregistré.
  await expect
    .poll(() => calls.filter((c) => c.method === 'GET' && c.path === '/members').length)
    .toBe(2);
  await expect(page.getByRole('combobox', { name: 'Rôle de member@saas.local' })).toHaveValue(
    'Admin',
  );
  await expect(
    page.getByRole('button', { name: 'Appliquer le rôle de member@saas.local' }),
  ).toBeDisabled();
});

// Dans Chromium, une flèche du clavier sur une liste fermée change sa valeur : rien ne doit
// partir avant « Appliquer ».
test('an arrow key on a role list sends nothing', async ({ page }) => {
  const calls = await mockOrganization(page, 'Owner');
  await page.goto('/organization');
  const select = page.getByRole('combobox', { name: 'Rôle de member@saas.local' });

  await select.focus();
  await page.keyboard.press('ArrowDown');

  await expect(
    page.getByRole('button', { name: 'Appliquer le rôle de member@saas.local' }),
  ).toBeEnabled();
  expect(calls.filter((c) => c.method === 'PUT')).toEqual([]);
});

test('an admin invites a colleague, and a duplicate is refused by its code', async ({ page }) => {
  await mockOrganization(page, 'Admin', ADMIN_ID);
  await page.goto('/organization');
  await expect(page.getByRole('button', { name: /^Retirer/ })).toHaveCount(0);

  await page.getByLabel('E-mail').fill('colleague@saas.local');
  await page.getByLabel('Rôle').selectOption('Admin');
  await page.getByRole('button', { name: 'Inviter' }).click();

  await expect(page.getByRole('status')).toHaveText('Invitation envoyée à colleague@saas.local.');
  await expect(page.getByRole('row', { name: /colleague@saas\.local/ })).toContainText('Vous');

  await page.getByLabel('E-mail').fill('colleague@saas.local');
  // Après un refus, la page relit la liste. Le test attend la réponse à cette relecture : sinon, la
  // requête encore en route à la fin du test sortirait des mocks.
  const readAgain = page.waitForResponse(
    (r) => r.request().method() === 'GET' && new URL(r.url()).pathname.endsWith('/invitations'),
  );
  await page.getByRole('button', { name: 'Inviter' }).click();

  await expect(page.getByRole('alert')).toHaveText(
    'Une invitation est déjà en attente pour cette adresse.',
  );
  await readAgain;
});

// « Déconnexion » n'est pas une session expirée : ni message, ni retour prévu sur l'écran.
test('logging out from the screen leads to the login page without a message', async ({ page }) => {
  await mockOrganization(page, 'Owner');
  await page.goto('/organization');

  await page.getByRole('button', { name: 'Déconnexion' }).click();

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByText('Votre session a expiré')).toHaveCount(0);
});

test('a member has no Organization link and is sent back to the products', async ({ page }) => {
  await mockOrganization(page, 'Member', MEMBER_ID);

  await page.goto('/organization');

  await expect(page).toHaveURL(/\/products$/);
  await expect(page.getByRole('link', { name: 'Produits' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Organisation' })).toHaveCount(0);
});
