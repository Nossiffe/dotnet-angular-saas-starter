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
  await expect(page.getByLabel('Prix')).toHaveValue('2.50');
  await name.fill('Stylo bleu');
  await page.keyboard.press('Enter');

  await expect(page.getByRole('status')).toHaveText('« Stylo bleu » est enregistré.');
  await expect(page.getByRole('row', { name: /Stylo bleu/ })).toContainText('€2.50');
  await expect(page.getByRole('button', { name: 'Modifier Stylo bleu' })).toBeFocused();
  // The emptied field lost the focus to the row's button: it shows no error.
  await expect(page.locator('mat-error')).toHaveCount(0);
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
  // The edit mode is rendered before the fields are filled.
  await expect(page.getByRole('form', { name: 'Modification de « Encre »' })).toBeVisible();
  await expect(page.getByLabel('Prix')).toHaveValue('4.00');
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
  // The edit mode is rendered before the field is filled.
  await expect(page.getByRole('form', { name: 'Modification de « Encre »' })).toBeVisible();
  await expect(page.getByLabel('Nom')).toHaveValue('Encre');
  await page.getByLabel('Nom').fill('Encre noire');
  await page.keyboard.press('Escape');

  await expect(page.getByRole('button', { name: 'Ajouter' })).toBeVisible();
  await expect(page.getByLabel('Nom')).toHaveValue('');
  await expect(page.getByLabel('Prix')).toHaveValue('');
  await expect(page.getByRole('button', { name: 'Modifier Encre' })).toBeFocused();
  await expect(page.locator('mat-error')).toHaveCount(0);
  expect(calls.filter((c) => c.method === 'PUT')).toEqual([]);
});

// The page shows its own messages, not the browser's bubble (the form has novalidate).
test('an empty form submitted says why nothing was added', async ({ page }) => {
  await mockProducts(page);
  await page.goto('/products');
  await expect(page.getByRole('button', { name: 'Modifier Encre' })).toBeVisible();

  await page.getByRole('button', { name: 'Ajouter' }).click();

  // Each message is visible and describes its field for a screen reader.
  await expect(page.getByLabel('Nom')).toHaveAccessibleDescription('Nom requis');
  await expect(page.getByLabel('Prix')).toHaveAccessibleDescription('Prix requis');
  await expect(page.getByText('Nom requis')).toBeVisible();
  await expect(page.getByText('Prix requis')).toBeVisible();
});

// « Modifier », une saisie et Échap dans la même tâche du navigateur : aucun rendu entre les trois.
// La remise à zéro vide quand même les champs (elle écrit le DOM elle-même).
test('Escape empties the fields even with no render since « Modifier »', async ({ page }) => {
  const calls = await mockProducts(page);
  await page.goto('/products');
  await expect(page.getByRole('button', { name: 'Modifier Encre' })).toBeVisible();

  await page.evaluate(() => {
    const [name, price] = Array.from(document.querySelectorAll<HTMLInputElement>('form input'));
    document.querySelector<HTMLButtonElement>('button[aria-label="Modifier Encre"]')!.click();
    name!.value = 'Encre noire';
    name!.dispatchEvent(new Event('input', { bubbles: true }));
    price!.value = '5';
    price!.dispatchEvent(new Event('input', { bubbles: true }));
    name!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
  });

  await expect(page.getByRole('button', { name: 'Ajouter' })).toBeVisible();
  await expect(page.getByLabel('Nom')).toHaveValue('');
  await expect(page.getByLabel('Prix')).toHaveValue('');
  expect(calls.filter((c) => c.method === 'PUT')).toEqual([]);
});

// Tapé vite, sans rendu entre deux touches : la page ne réécrit pas la saisie en cours. Avec un
// champ numérique, « 3. » devenait « 3 » et le curseur partait au début (« 503 »).
test('a price typed quickly keeps every character', async ({ page }) => {
  const calls = await mockProducts(page);
  await page.goto('/products');
  await page.getByRole('button', { name: 'Modifier Encre' }).click();
  await expect(page.getByRole('form', { name: 'Modification de « Encre »' })).toBeVisible();
  const price = page.getByLabel('Prix');
  await price.fill('');

  await price.pressSequentially('3.50');

  await expect(price).toHaveValue('3.50');
  await page.keyboard.press('Enter');
  await expect(page.getByRole('row', { name: /Encre/ })).toContainText('€3.50');
  expect(calls).toContainEqual({
    method: 'PUT',
    path: `/products/${INK_ID}`,
    body: { name: 'Encre', price: 3.5 },
  });
});
