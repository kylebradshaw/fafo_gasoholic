import { test, expect, request } from '@playwright/test';
import { devLogin, uniqueEmail } from '../helpers/auth';

const BASE = process.env.BASE_URL ?? 'http://localhost:5100';

test.describe('Fillup log tab', () => {
  let cookieState: Awaited<ReturnType<typeof devLogin>>;
  let autoId: number;
  let partialId: number;

  test.beforeAll(async () => {
    const api = await request.newContext({ baseURL: BASE });
    cookieState = await devLogin(api, uniqueEmail('log'));

    const autoRes = await api.post('/api/autos', {
      data: { brand: 'LogTest', model: 'Car', plate: 'LOG-001', odometer: 10000 },
    });
    autoId = (await autoRes.json()).id;

    await api.post(`/api/autos/${autoId}/fillups`, { data: {
      filledAt: '2026-03-01T08:00:00', fuelType: 0,
      pricePerGallon: 3.5, gallons: 10, odometer: 10200, isPartialFill: false,
    }});
    await api.post(`/api/autos/${autoId}/fillups`, { data: {
      filledAt: '2026-03-05T10:00:00', fuelType: 2,
      pricePerGallon: 3.9, gallons: 12, odometer: 10500, isPartialFill: false,
    }});
    const partialRes = await api.post(`/api/autos/${autoId}/fillups`, { data: {
      filledAt: '2026-03-08T12:00:00', fuelType: 0,
      pricePerGallon: 3.6, gallons: 5, odometer: 10650, isPartialFill: true,
    }});
    partialId = (await partialRes.json()).id;

    await api.dispose();
  });

  async function gotoWithAuto(page: import('@playwright/test').Page, context: import('@playwright/test').BrowserContext) {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');
    await page.selectOption('#autoSelector', String(autoId));
  }

  test('table renders fillups newest first', async ({ page, context }) => {
    await gotoWithAuto(page, context);
    const rows = page.locator('tbody tr');
    await expect(rows).toHaveCount(3);
    await expect(rows.first()).toContainText('Exxon');
  });

  test('switching auto selector reloads table', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');
    await page.selectOption('#autoSelector', String(autoId));
    await expect(page.locator('tbody tr')).toHaveCount(3);
    await page.selectOption('#autoSelector', '');
    await expect(page.locator('#fillupContent')).toContainText('Select an auto');
  });

  test('partial fill shows dash, full fill shows MPG', async ({ page, context }) => {
    await gotoWithAuto(page, context);
    const rows = page.locator('tbody tr');
    await expect(rows.nth(0).locator('td').nth(6)).toHaveText('—');
    const mpgText = await rows.nth(1).locator('td').nth(6).textContent();
    expect(mpgText).not.toBe('—');
    expect(parseFloat(mpgText!)).toBeGreaterThan(0);
  });

  test('delete row removes it without page reload', async ({ page, context }) => {
    await gotoWithAuto(page, context);
    await expect(page.locator('tbody tr')).toHaveCount(3);
    await page.locator('tbody tr').first().getByRole('button', { name: 'Del' }).click();
    await expect(page.locator('tbody tr')).toHaveCount(2);
  });

  test('table is scrollable on 375px mobile', async ({ page, context }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await gotoWithAuto(page, context);
    await expect(page.locator('.table-wrap')).toBeVisible();
    const bodyScroll = await page.evaluate(() => document.body.scrollWidth);
    expect(bodyScroll).toBeLessThanOrEqual(375);
  });
});
