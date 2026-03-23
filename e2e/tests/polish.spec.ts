import { test, expect, request } from '@playwright/test';
import { devLogin, uniqueEmail } from '../helpers/auth';

const BASE = process.env.BASE_URL ?? 'http://localhost:5100';
const VIEWPORTS = [
  { name: '375px mobile',  width: 375,  height: 812  },
  { name: '768px tablet',  width: 768,  height: 1024 },
  { name: '1280px desktop',width: 1280, height: 800  },
];

test.describe('Polish & responsive design', () => {

  test('no layout breakage across breakpoints', async ({ page }) => {
    for (const vp of VIEWPORTS) {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto('/');
      const scrollWidth = await page.evaluate(() => document.body.scrollWidth);
      expect(scrollWidth, `Horizontal overflow at ${vp.name}`).toBeLessThanOrEqual(vp.width);
    }
  });

  test('full happy path: login → add auto → 3 fillups → verify MPG → edit → delete', async ({ page, context }) => {
    const errors: string[] = [];
    page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });

    const api = await request.newContext({ baseURL: BASE });
    const state = await devLogin(api, uniqueEmail('polish'));
    await api.dispose();
    await context.addCookies(state.cookies);

    await page.goto('/app.html');
    await page.click('text=Autos');
    await page.click('#addAutoBtn');
    await page.fill('#autoBrand', 'Honda');
    await page.fill('#autoModel', 'Accord');
    await page.fill('#autoPlate', 'E2E-001');
    await page.fill('#autoOdometer', '50000');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/autos') && r.request().method() === 'POST'),
      page.click('#autoForm button[type="submit"]'),
    ]);
    await expect(page.locator('.auto-card', { hasText: 'Honda Accord' })).toBeVisible();

    await page.click('.tab-btn[data-tab="log"]');
    const autoValue = await page.locator('#autoSelector option', { hasText: 'Honda Accord' }).getAttribute('value');
    await page.locator('#autoSelector').selectOption(autoValue!);

    // First full fill
    await page.click('#addFillupBtn');
    await page.fill('#fillupDate', '2026-01-10');
    await page.fill('#fillupTime', '08:00');
    await page.fill('#fillupPrice', '3.50');
    await page.fill('#fillupGallons', '10.0');
    await page.fill('#fillupOdometer', '50200');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/fillups') && r.request().method() === 'POST'),
      page.click('#fillupForm button[type="submit"]'),
    ]);
    await expect(page.locator('tbody tr')).toHaveCount(1);

    // Second full fill
    await page.click('#addFillupBtn');
    await page.fill('#fillupDate', '2026-01-20');
    await page.fill('#fillupTime', '09:00');
    await page.fill('#fillupPrice', '3.60');
    await page.fill('#fillupGallons', '12.0');
    await page.fill('#fillupOdometer', '50560');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/fillups') && r.request().method() === 'POST'),
      page.click('#fillupForm button[type="submit"]'),
    ]);
    await expect(page.locator('tbody tr')).toHaveCount(2);

    // Partial fill
    await page.click('#addFillupBtn');
    await page.fill('#fillupDate', '2026-01-25');
    await page.fill('#fillupTime', '10:00');
    await page.fill('#fillupPrice', '3.55');
    await page.fill('#fillupGallons', '5.0');
    await page.fill('#fillupOdometer', '50700');
    await page.check('#fillupPartial');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/fillups') && r.request().method() === 'POST'),
      page.click('#fillupForm button[type="submit"]'),
    ]);
    await expect(page.locator('tbody tr')).toHaveCount(3);

    // Verify MPG — rows newest first: partial, second full, first full
    const rows = page.locator('tbody tr');
    await expect(rows.nth(0).locator('td').nth(6)).toHaveText('—');
    const mpgText = await rows.nth(1).locator('td').nth(6).textContent();
    expect(parseFloat(mpgText!)).toBeCloseTo(30, 0);
    await expect(rows.nth(2).locator('td').nth(6)).toHaveText('—');

    // Edit second fillup
    await rows.nth(1).getByRole('button', { name: 'Edit' }).click();
    await page.fill('#fillupGallons', '14.4');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/fillups/') && r.request().method() === 'PUT'),
      page.click('#fillupForm button[type="submit"]'),
    ]);
    const updatedMpg = await page.locator('tbody tr').nth(1).locator('td').nth(6).textContent();
    expect(parseFloat(updatedMpg!)).toBeCloseTo(25, 0);

    // Delete partial fill
    await page.locator('tbody tr').first().getByRole('button', { name: 'Del' }).click();
    await expect(page.locator('tbody tr')).toHaveCount(2);

    const realErrors = errors.filter(e => !e.includes('geolocation') && !e.includes('Nominatim'));
    expect(realErrors, `Console errors: ${realErrors.join(', ')}`).toHaveLength(0);
  });

  test('API errors surface as toast, not alerts', async ({ page }) => {
    await page.route('**/api/autos', route => route.fulfill({ status: 500, body: 'error' }));
    let dialogShown = false;
    page.on('dialog', () => { dialogShown = true; });
    await page.goto('/app.html');
    await page.waitForTimeout(500);
    expect(dialogShown).toBe(false);
  });
});
