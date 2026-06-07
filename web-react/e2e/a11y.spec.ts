import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

test('page login sans violation a11y critique', async ({ page }) => {
    await page.goto('/login');
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
    const blocking = results.violations.filter(
        (v) => v.impact === 'critical' || v.impact === 'serious',
    );
    expect(blocking).toEqual([]);
});
