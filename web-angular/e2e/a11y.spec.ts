import { test, expect } from './fixtures';

test('page login sans violation a11y critique', async ({ page, makeAxeBuilder }) => {
  await page.goto('/login');
  const results = await makeAxeBuilder().analyze();
  const blocking = results.violations.filter(
    (v) => v.impact === 'critical' || v.impact === 'serious',
  );
  expect(blocking).toEqual([]);
});
