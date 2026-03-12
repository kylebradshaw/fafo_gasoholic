// @ts-check
const { test, expect, request } = require('@playwright/test');

const BASE = 'http://localhost:5100';

async function apiSetup() {
  const api = await request.newContext({ baseURL: BASE });
  const email = `task8-${Date.now()}@test.com`;
  await api.post('/auth/login', { data: { email } });
  const state = await api.storageState();

  // Create auto
  const autoRes = await api.post('/api/autos', {
    data: { brand: 'Task8', model: 'Car', plate: 'T8-TEST', odometer: 10000 }
  });
  const { id: autoId } = await autoRes.json();

  // Create fillups: full fill at 10200, then partial at 10350
  await api.post(`/api/autos/${autoId}/fillups`, { data: {
    filledAt: '2026-03-01T08:00:00', location: 'Shell', latitude: null, longitude: null,
    fuelType: 0, pricePerGallon: 3.5, gallons: 10, odometer: 10200, isPartialFill: false
  }});
  await api.post(`/api/autos/${autoId}/fillups`, { data: {
    filledAt: '2026-03-05T10:00:00', location: 'BP', latitude: null, longitude: null,
    fuelType: 2, pricePerGallon: 3.9, gallons: 12, odometer: 10500, isPartialFill: false
  }});
  const partialRes = await api.post(`/api/autos/${autoId}/fillups`, { data: {
    filledAt: '2026-03-08T12:00:00', location: 'Exxon', latitude: null, longitude: null,
    fuelType: 0, pricePerGallon: 3.6, gallons: 5, odometer: 10650, isPartialFill: true
  }});
  const { id: partialId } = await partialRes.json();

  await api.dispose();
  return { state, autoId, partialId };
}

test.describe('Task 8 — Fillup Log Tab', () => {
  let setup;

  test.beforeAll(async () => {
    setup = await apiSetup();
  });

  async function gotoAppWithAuto(page, context) {
    await context.addCookies(setup.state.cookies);
    await page.goto('/app.html');
    // Select the auto
    await page.selectOption('#autoSelector', String(setup.autoId));
  }

  test('table renders fillups for selected auto, newest first', async ({ page, context }) => {
    await gotoAppWithAuto(page, context);
    const rows = page.locator('tbody tr');
    await expect(rows).toHaveCount(3);
    // First row should be most recent (2026-03-08 Exxon partial)
    await expect(rows.first()).toContainText('Exxon');
  });

  test('switching auto selector reloads table', async ({ page, context }) => {
    await context.addCookies(setup.state.cookies);
    await page.goto('/app.html');

    // Create second auto via UI
    const api = await request.newContext({ baseURL: BASE });
    // reuse cookies
    for (const c of setup.state.cookies) {
      await api.storageState(); // just to check
    }
    // Create via API with same session
    const api2 = await request.newContext({ baseURL: BASE });
    await api2.post('/auth/login', { data: { email: `task8b-${Date.now()}@test.com` } });
    const stateB = await api2.storageState();
    await api2.dispose();

    // Switch selector to "no auto" first
    await context.addCookies(setup.state.cookies);
    await page.goto('/app.html');
    await page.selectOption('#autoSelector', String(setup.autoId));
    await expect(page.locator('tbody tr')).toHaveCount(3);

    // Switch to empty
    await page.selectOption('#autoSelector', '');
    await expect(page.locator('#fillupContent')).toContainText('Select an auto');
  });

  test('MPG shows computed value or dash correctly', async ({ page, context }) => {
    await gotoAppWithAuto(page, context);
    const rows = page.locator('tbody tr');
    // Partial (first row, most recent) should show —
    await expect(rows.nth(0).locator('td').nth(6)).toHaveText('—');
    // Second full fill should have MPG
    const mpgCell = rows.nth(1).locator('td').nth(6);
    const mpgText = await mpgCell.textContent();
    expect(mpgText).not.toBe('—');
    expect(parseFloat(mpgText)).toBeGreaterThan(0);
  });

  test('delete row removes it without page reload', async ({ page, context }) => {
    await gotoAppWithAuto(page, context);
    // Wait for table to render
    await expect(page.locator('tbody tr')).toHaveCount(3);

    // Delete the partial fill row (first row, Exxon)
    await page.locator('tbody tr').first().getByRole('button', { name: 'Del' }).click();
    await expect(page.locator('tbody tr')).toHaveCount(2);
    await expect(page.locator('tbody')).not.toContainText('Exxon');
  });

  test('table scrollable on 375px mobile', async ({ page, context }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await gotoAppWithAuto(page, context);

    // Table should be inside a scrollable container
    const wrap = page.locator('.table-wrap');
    await expect(wrap).toBeVisible();
    // Body should not overflow horizontally beyond viewport
    const bodyScroll = await page.evaluate(() => document.body.scrollWidth);
    expect(bodyScroll).toBeLessThanOrEqual(375);
  });
});
