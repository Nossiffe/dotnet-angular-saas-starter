import { test, expect } from './fixtures';

test('affiche le formulaire de connexion', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('button', { name: /se connecter/i })).toBeVisible();
});
