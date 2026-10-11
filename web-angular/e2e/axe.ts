import { expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** The rules of every axe check in the project: WCAG 2.2 A and AA. */
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

/**
 * Waits until the open snack-bars have been announced.
 *
 * MatSnackBarContainer (@angular/material 22.2, snack-bar.mjs) first renders its content, the
 * action button included, in a <div aria-hidden="true">. 150 ms after the snack-bar enters the
 * screen (_announceDelay; a queued snack-bar enters once the previous one has left), it removes
 * aria-hidden and moves that div into its aria-live region, so that screen readers announce the
 * message. axe run before that reports aria-hidden-focus on that div, which holds a focusable
 * button: a passing state of Material, not a defect of the page.
 *
 * The wait is a condition, never a fixed delay: no such aria-hidden div left, the element axe would
 * flag. It covers the snack-bars already open when the check starts. A test that expects one waits
 * for its text before checking, as the journey does; a snack-bar that opens while axe runs is not
 * covered.
 */
export async function untilSnackBarsAnnounced(page: Page): Promise<void> {
  await expect(
    // Anchored on Material's template: an aria-hidden element of the content cannot match.
    page.locator(
      'mat-snack-bar-container > .mat-mdc-snackbar-surface > .mat-mdc-snack-bar-label > [aria-hidden]',
    ),
    'snack-bars still waiting to be announced',
  ).toHaveCount(0);
}

/** An axe builder on WCAG 2.2 A and AA whose analyze() first waits for the snack-bars. */
class AnnouncedAxeBuilder extends AxeBuilder {
  private readonly checkedPage: Page;

  constructor(page: Page) {
    super({ page });
    this.checkedPage = page;
    this.withTags(WCAG_TAGS);
  }

  override async analyze(): ReturnType<AxeBuilder['analyze']> {
    await untilSnackBarsAnnounced(this.checkedPage);
    return super.analyze();
  }
}

export function axeBuilder(page: Page): AxeBuilder {
  return new AnnouncedAxeBuilder(page);
}
