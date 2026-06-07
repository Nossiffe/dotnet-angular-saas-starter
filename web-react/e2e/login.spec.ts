import { test, expect } from '@playwright/test';
test("le formulaire de connexion s'affiche", async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('button', { name: /se connecter/i })).toBeVisible();
});
