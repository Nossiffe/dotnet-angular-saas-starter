import { test as base } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

export const test = base.extend<{ makeAxeBuilder: () => AxeBuilder }>({
    makeAxeBuilder: async ({ page }, use) => {
        await use(() =>
            new AxeBuilder({ page }).withTags([
                'wcag2a',
                'wcag2aa',
                'wcag21a',
                'wcag21aa',
                'wcag22aa',
            ]),
        );
    },
});
export { expect } from '@playwright/test';
