import { test, expect, request } from '@playwright/test';
import { devLogin, uniqueEmail } from '../helpers/auth';

const BASE = process.env.BASE_URL ?? 'http://localhost:5100';

test.describe('Add/edit fillup modal', () => {
  let cookieState: Awaited<ReturnType<typeof devLogin>>;
  let autoId: number;

  test.beforeAll(async () => {
    const api = await request.newContext({ baseURL: BASE });
    cookieState = await devLogin(api, uniqueEmail('modal'));
    const autoRes = await api.post('/api/autos', {
      data: { brand: 'ModalTest', model: 'Car', plate: 'MOD-001', odometer: 20000 },
    });
    autoId = (await autoRes.json()).id;
    await api.dispose();
  });

  async function gotoWithAuto(page: import('@playwright/test').Page, context: import('@playwright/test').BrowserContext) {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');
    await page.selectOption('#autoSelector', String(autoId));
  }

  test('GPS denied — location field is empty and editable', async ({ page, context }) => {
    await context.grantPermissions([]);
    await gotoWithAuto(page, context);
    await page.click('#addFillupBtn');
    await expect(page.locator('#fillupModal.open')).toBeVisible();
    const locationField = page.locator('#fillupLocation');
    await expect(locationField).toBeEditable();
    await page.waitForTimeout(500);
    expect(await locationField.inputValue()).toBe('');
  });

  test('saving new fillup appends to top of log', async ({ page, context }) => {
    await gotoWithAuto(page, context);
    const rowsBefore = await page.locator('tbody tr').count();
    await page.click('#addFillupBtn');
    await expect(page.locator('#fillupModal.open')).toBeVisible();
    await page.fill('#fillupDate', '2026-03-10');
    await page.fill('#fillupTime', '09:00');
    await page.fill('#fillupPrice', '3.75');
    await page.fill('#fillupGallons', '11.5');
    await page.fill('#fillupOdometer', '20300');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/fillups') && r.request().method() === 'POST'),
      page.click('#fillupForm button[type="submit"]'),
    ]);
    await expect(page.locator('tbody tr')).toHaveCount(rowsBefore + 1);
    await expect(page.locator('tbody tr').first()).toContainText('3/10/2026');
  });

  test('editing a fillup updates the row in place', async ({ page, context }) => {
    await gotoWithAuto(page, context);
    await expect(page.locator('tbody tr')).toHaveCount(1);
    await page.locator('tbody tr').first().getByRole('button', { name: 'Edit' }).click();
    await expect(page.locator('#fillupModal.open')).toBeVisible();
    expect(await page.locator('#fillupId').inputValue()).not.toBe('');
    await page.fill('#fillupGallons', '15.0');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/fillups/') && r.request().method() === 'PUT'),
      page.click('#fillupForm button[type="submit"]'),
    ]);
    await expect(page.locator('tbody tr').first()).toContainText('15.000');
  });

  test('required fields validated before submit', async ({ page, context }) => {
    await gotoWithAuto(page, context);
    await page.click('#addFillupBtn');
    await expect(page.locator('#fillupModal.open')).toBeVisible();
    await page.fill('#fillupPrice', '');
    await page.fill('#fillupGallons', '');
    await page.fill('#fillupOdometer', '');
    await page.click('#fillupForm button[type="submit"]');
    await expect(page.locator('#fillupModal.open')).toBeVisible();
  });
});
