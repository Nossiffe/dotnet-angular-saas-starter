import { test, expect } from './fixtures';
import { INVITATION_LINK, mockNewAccount } from './invitation-mocks';
import { ADMIN_ID, mockOrganization } from './organization-mocks';
import { mockProducts } from './products-mocks';

// axe checks the rendered form: it runs once the submit button is visible. Every violation of the
// WCAG A and AA rules fails the test, whatever its impact.
test('the login page has no WCAG 2.2 A or AA violation found by axe', async ({
  page,
  makeAxeBuilder,
}) => {
  await page.goto('/login');
  await expect(page.getByRole('button', { name: /se connecter/i })).toBeVisible();

  const results = await makeAxeBuilder().analyze();

  expect(results.violations).toEqual([]);
});

test('the password step of an invitation has no WCAG 2.2 A or AA violation found by axe', async ({
  page,
  makeAxeBuilder,
}) => {
  await mockNewAccount(page);
  await page.goto(INVITATION_LINK);
  await expect(page.getByRole('button', { name: /créer mon compte/i })).toBeVisible();

  const results = await makeAxeBuilder().analyze();

  expect(results.violations).toEqual([]);
});

test('the Organization screen has no WCAG 2.2 A or AA violation found by axe', async ({
  page,
  makeAxeBuilder,
}) => {
  await mockOrganization(page, 'Owner');
  await page.goto('/organization');
  await expect(page.getByRole('button', { name: 'Inviter' })).toBeVisible();
  await expect(page.getByRole('table', { name: 'Invitations en attente' })).toBeVisible();

  const results = await makeAxeBuilder().analyze();

  expect(results.violations).toEqual([]);
});

test('the removal confirmation has no WCAG 2.2 A or AA violation found by axe', async ({
  page,
  makeAxeBuilder,
}) => {
  await mockOrganization(page, 'Owner');
  await page.goto('/organization');
  await page.getByRole('button', { name: 'Retirer admin@saas.local' }).click();
  await expect(page.getByRole('checkbox', { name: 'Les révoquer aussi' })).toBeVisible();
  // axe lit les couleurs : il attend la fin de l'animation d'ouverture du dialogue.
  await expect(page.locator('mat-dialog-container')).toHaveClass(/mdc-dialog--open/);
  await expect(page.locator('mat-dialog-container')).not.toHaveClass(/mdc-dialog--opening/);

  const results = await makeAxeBuilder().analyze();

  expect(results.violations).toEqual([]);
});

test('the products page, with a product being edited, has no WCAG 2.2 A or AA violation found by axe', async ({
  page,
  makeAxeBuilder,
}) => {
  await mockProducts(page);
  await page.goto('/products');
  await page.getByRole('button', { name: 'Modifier Stylo' }).click();
  await expect(page.getByRole('button', { name: 'Enregistrer' })).toBeVisible();

  const results = await makeAxeBuilder().analyze();

  expect(results.violations).toEqual([]);
});

// À 360 px de large, le tableau des membres déborde de sa carte et défile dedans. Vu par un Admin,
// il n'a ni bouton ni liste : c'est son conteneur qui reçoit le focus, pour le faire défiler au
// clavier. À la largeur d'un bureau, les tableaux tiennent dans leur carte : le conteneur qui a le
// focus le garde, puis sort de l'ordre du clavier une fois quitté ; l'autre en sort aussitôt.
test('on a narrow screen, a table wider than its card can be scrolled from the keyboard, and axe finds no violation', async ({
  page,
  makeAxeBuilder,
}) => {
  await page.setViewportSize({ width: 360, height: 800 });
  await mockOrganization(page, 'Admin', ADMIN_ID);
  await page.goto('/organization');
  await expect(page.getByRole('table', { name: 'Invitations en attente' })).toBeVisible();
  const members = page.getByRole('group', { name: "Membres de l'organisation" });
  const invitations = page.getByRole('group', { name: 'Invitations en attente' });
  await expect(members).toHaveAttribute('tabindex', '0');
  await expect(invitations).toHaveAttribute('tabindex', '0');

  const results = await makeAxeBuilder().analyze();

  expect(results.violations).toEqual([]);

  await members.focus();
  await page.setViewportSize({ width: 1280, height: 720 });
  await expect(invitations).toHaveCount(0);
  await expect(members).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(members).toHaveCount(0);
  await expect(page.locator('.table-scroll[tabindex]')).toHaveCount(0);
});
