import { test, expect } from '@playwright/test';

test('affiche le formulaire de connexion', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('button', { name: /se connecter/i })).toBeVisible();
});
