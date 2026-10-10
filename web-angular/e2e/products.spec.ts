import { test, expect } from './fixtures';
import { INK_ID, PEN_ID, mockProducts } from './products-mocks';

// Le prix de 2,50 € reste tel quel : un champ numérique sans pas décimal refuserait ce formulaire.
test('a product is edited from the keyboard, and the focus returns to it', async ({ page }) => {
  const calls = await mockProducts(page);
  await page.goto('/products');

  await page.getByRole('button', { name: 'Modifier Stylo' }).focus();
  await page.keyboard.press('Enter');
  const name = page.getByLabel('Nom');
  await expect(name).toBeFocused();
  await expect(page.getByRole('form', { name: 'Modification de « Stylo »' })).toBeVisible();
  await expect(page.getByLabel('Prix')).toHaveValue('2.5');
  await name.fill('Stylo bleu');
  await page.keyboard.press('Enter');

  await expect(page.getByRole('status')).toHaveText('« Stylo bleu » est enregistré.');
  await expect(page.getByRole('row', { name: /Stylo bleu/ })).toContainText('€2.50');
  await expect(page.getByRole('button', { name: 'Modifier Stylo bleu' })).toBeFocused();
  expect(calls).toContainEqual({
    method: 'PUT',
    path: `/products/${PEN_ID}`,
    body: { name: 'Stylo bleu', price: 2.5 },
  });
});

test('a price with cents is entered and saved', async ({ page }) => {
  const calls = await mockProducts(page);
  await page.goto('/products');

  await page.getByRole('button', { name: 'Modifier Encre' }).click();
  await page.getByLabel('Prix').fill('3.99');
  await page.getByRole('button', { name: 'Enregistrer' }).click();

  await expect(page.getByRole('row', { name: /Encre/ })).toContainText('€3.99');
  expect(calls).toContainEqual({
    method: 'PUT',
    path: `/products/${INK_ID}`,
    body: { name: 'Encre', price: 3.99 },
  });
});

test('Escape cancels an edit without sending it', async ({ page }) => {
  const calls = await mockProducts(page);
  await page.goto('/products');

  await page.getByRole('button', { name: 'Modifier Encre' }).click();
  await page.getByLabel('Nom').fill('Encre noire');
  await page.keyboard.press('Escape');

  await expect(page.getByRole('button', { name: 'Ajouter' })).toBeVisible();
  await expect(page.getByLabel('Nom')).toHaveValue('');
  await expect(page.getByRole('button', { name: 'Modifier Encre' })).toBeFocused();
  expect(calls.filter((c) => c.method === 'PUT')).toEqual([]);
});
