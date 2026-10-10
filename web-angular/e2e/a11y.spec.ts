import { test, expect } from './fixtures';
import { INVITATION_LINK, mockNewAccount } from './invitation-mocks';
import { mockOrganization } from './organization-mocks';
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
