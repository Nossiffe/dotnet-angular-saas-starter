import { test, expect } from './fixtures';

test('shows the login form', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('button', { name: /se connecter/i })).toBeVisible();
});
