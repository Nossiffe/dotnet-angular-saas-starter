import { test, expect } from './fixtures';

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
