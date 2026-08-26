import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

const password = 'E2eTest123!';

const viewports = [
  { width: 390, height: 844 },
  { width: 768, height: 1024 },
  { width: 1280, height: 900 },
];

async function expectResponsiveAndAccessible(page: Page) {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.waitForTimeout(25);

  const animationDurations = await page.evaluate(() =>
    [...document.querySelectorAll<HTMLElement>('.page-enter, [class*="animate-"]')].flatMap(
      (element) =>
        getComputedStyle(element)
          .animationDuration.split(',')
          .map((duration) =>
            duration.trim().endsWith('ms')
              ? Number.parseFloat(duration)
              : Number.parseFloat(duration) * 1000,
          ),
    ),
  );
  expect(animationDurations.every((duration) => duration <= 1)).toBe(true);
  await expect(page.locator('main')).toHaveCount(1);
  await expect(page.locator('h1')).toHaveCount(1);

  for (const viewport of viewports) {
    await page.setViewportSize(viewport);

    const layout = await page.evaluate(() => ({
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      overflowingElements: [...document.querySelectorAll<HTMLElement>('body *')]
        .map((element) => {
          const bounds = element.getBoundingClientRect();
          return {
            element: `${element.tagName.toLowerCase()}${element.id ? `#${element.id}` : ''}.${[
              ...element.classList,
            ]
              .slice(0, 3)
              .join('.')}`,
            left: Math.round(bounds.left),
            right: Math.round(bounds.right),
          };
        })
        .filter(
          (element) => element.left < 0 || element.right > document.documentElement.clientWidth,
        ),
      smallTargets: [...document.querySelectorAll<HTMLElement>('button:not([disabled]), a[href]')]
        .filter((element) => {
          const style = getComputedStyle(element);
          return style.display !== 'none' && style.visibility !== 'hidden';
        })
        .map((element) => {
          const bounds = element.getBoundingClientRect();
          return {
            name:
              element.getAttribute('aria-label') || element.textContent?.trim() || element.tagName,
            width: Math.round(bounds.width),
            height: Math.round(bounds.height),
          };
        })
        .filter((target) => target.width < 44 || target.height < 44),
    }));

    expect(
      layout.scrollWidth,
      JSON.stringify(layout.overflowingElements, null, 2),
    ).toBeLessThanOrEqual(layout.clientWidth);
    expect(layout.smallTargets, `Tryckytor vid ${viewport.width} px`).toEqual([]);
  }

  await page.setViewportSize(viewports[0]);
  const accessibilityScan = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(
    accessibilityScan.violations,
    JSON.stringify(accessibilityScan.violations, null, 2),
  ).toEqual([]);
}

async function registerAndSignInAdult(page: Page, runId: string) {
  const email = `e2e-${runId}@example.test`;

  await page.goto('/login');
  await expectResponsiveAndAccessible(page);
  await page.keyboard.press('Tab');
  await expect(page.locator(':focus')).toHaveCSS('outline-style', 'solid');
  await page.getByRole('button', { name: 'Skapa konto' }).click();
  await expectResponsiveAndAccessible(page);
  await page.getByLabel('Familjens namn').fill(`E2E Familj ${runId}`);
  await page.getByLabel('Din e-post').fill(email);
  await page.getByLabel('Välj lösenord').fill(password);
  await page.getByLabel('Upprepa lösenordet').fill(password);
  await page.getByRole('button', { name: 'Skapa familj och konto' }).click();

  await expect(page.getByRole('heading', { name: 'Välkommen till Syssloappen!' })).toBeVisible();
  await page.getByRole('button', { name: 'Jag har sparat koden – fortsätt' }).click();
  await page.getByLabel('E-post').fill(email);
  await page.getByLabel('Lösenord').fill(password);
  await page.getByRole('button', { name: 'Logga in som vuxen' }).click();
  await expect(page).toHaveURL(/\/vuxen$/);
  await expectResponsiveAndAccessible(page);
}

async function createChildAndGetPairingCode(page: Page, runId: string, childName: string) {
  await page.goto('/vuxen/barn');
  await page.getByRole('button', { name: '+ Lägg till barn' }).click();
  await expect(page.locator('#create-child-panel')).toBeFocused();
  await page.getByLabel('Barnets namn').fill(childName);
  await page.getByLabel('Användarnamn').fill(`e2e-${runId}`);
  await page.getByLabel(/^Lösenord/).fill(password);
  await page.getByLabel('Upprepa lösenordet').fill(password);
  await page.getByRole('button', { name: 'Skapa barnkonto' }).click();

  await expect(page.getByRole('heading', { name: `${childName} är tillagd` })).toBeVisible();
  await page.getByRole('button', { name: 'Koppla enhet' }).first().click();

  const pairingSection = page.locator('section[aria-labelledby="pairing-code-title"]');
  await expect(pairingSection).toBeVisible();
  await expect(pairingSection).toBeFocused();
  await expectResponsiveAndAccessible(page);
  const sectionText = await pairingSection.innerText();
  const pairingCode = sectionText.match(/\b[A-Z0-9]{8}\b/)?.[0];
  expect(pairingCode, 'En åttateckens engångskod ska visas').toBeTruthy();
  return pairingCode!;
}

async function createAndAssignChore(page: Page, choreTitle: string, childName: string) {
  await page.goto('/vuxen/sysslor');
  await page.getByRole('button', { name: '+ Ny syssla' }).click();
  await expect(page.locator('#new-chore-panel')).toBeFocused();
  await page.getByLabel('Titel').fill(choreTitle);
  await page.getByLabel('Poäng').selectOption({ label: '10 poäng' });
  await page.getByLabel(/Beskrivning/).fill('E2E-test av hela syssleflödet.');
  await page.getByRole('button', { name: 'Skapa syssla' }).click();

  await expect(page.getByRole('heading', { name: 'Tilldela syssla' })).toBeVisible();
  await expect(page.locator('#assignment-panel')).toBeFocused();
  await page.getByLabel('Barn').selectOption({ label: childName });
  await page.getByRole('button', { name: 'Tilldela sysslan' }).click();
  await expect(page.getByRole('status')).toContainText('tilldelad');
  await expectResponsiveAndAccessible(page);
}

test('hela syssleflödet fungerar mellan Adult och Child', async ({ browser }) => {
  const runId = `${Date.now()}-${Math.floor(Math.random() * 10_000)}`;
  const childName = `E2E Barn ${runId}`;
  const choreTitle = `E2E Syssla ${runId}`;
  const redoComment = 'E2E: Kom ihåg att ställa tillbaka skålen.';
  const adultContext = await browser.newContext();
  const childContext = await browser.newContext();
  const adultPage = await adultContext.newPage();
  const childPage = await childContext.newPage();

  await registerAndSignInAdult(adultPage, runId);
  const pairingCode = await createChildAndGetPairingCode(adultPage, runId, childName);
  await createAndAssignChore(adultPage, choreTitle, childName);

  await childPage.goto('/login');
  await childPage.getByRole('button', { name: 'Barn' }).click();
  await expect(childPage.getByLabel('Kod från en vuxen')).toBeVisible();
  await childPage.getByLabel('Kod från en vuxen').fill(pairingCode);
  await childPage.getByRole('button', { name: 'Koppla min enhet' }).click();
  await expect(childPage).toHaveURL(/\/barn$/);

  const pointsSection = childPage.locator('section[aria-labelledby="points-title"]');
  const childChore = childPage.getByRole('article').filter({ hasText: choreTitle });
  await expectResponsiveAndAccessible(childPage);
  await expect(childChore).toContainText('Att göra');
  await expect(pointsSection.getByLabel('0 poäng')).toBeVisible();
  await childPage.getByRole('button', { name: `Rapportera ${choreTitle} som klar` }).click();
  await expect(childChore).toContainText('Väntar på godkännande');

  await adultPage.goto('/vuxen/granska');
  const pendingReviewSection = adultPage.locator('section[aria-labelledby="pending-review-title"]');
  const adultReview = pendingReviewSection.getByRole('article').filter({ hasText: choreTitle });
  await expectResponsiveAndAccessible(adultPage);
  await expect(adultReview).toContainText(childName);
  await adultReview.getByLabel(/Kommentar/).fill(redoComment);
  await adultReview.getByRole('button', { name: `Be ${childName} göra om ${choreTitle}` }).click();
  await expect(adultReview).toBeHidden();
  await expect(adultPage.locator('#review-success-message')).toBeFocused();

  await childPage.reload();
  await expect(childChore).toContainText('Behöver göras om');
  await expect(childChore).toContainText(redoComment);
  await childPage.getByRole('button', { name: `Rapportera ${choreTitle} som klar` }).click();
  await expect(childChore).toContainText('Väntar på godkännande');
  await expect(childChore).toBeFocused();
  await expect(pointsSection.getByLabel('0 poäng')).toBeVisible();

  await adultPage.reload();
  await expect(adultReview).toBeVisible();
  await adultReview.getByRole('button', { name: `Godkänn ${choreTitle} för ${childName}` }).click();
  await expect(adultReview).toBeHidden();

  await childPage.reload();
  await expect(childChore).toContainText('Godkänd');
  await expect(pointsSection.getByLabel('10 poäng')).toBeVisible();
});
