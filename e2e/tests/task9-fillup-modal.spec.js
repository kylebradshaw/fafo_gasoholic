// @ts-check
const { test, expect, request } = require('@playwright/test');

const BASE = 'http://localhost:5100';

async function setupWithAuto() {
  const api = await request.newContext({ baseURL: BASE });
  const email = `task9-${Date.now()}@test.com`;
  await api.post('/auth/login', { data: { email } });
  const state = await api.storageState();
  const autoRes = await api.post('/api/autos', {
    data: { brand: 'Task9', model: 'Car', plate: 'T9', odometer: 20000 }
  });
  const { id: autoId } = await autoRes.json();
  await api.dispose();
  return { state, autoId };
}

test.describe('Task 9 — Add/Edit Fillup Modal', () => {
  let setup;

  test.beforeAll(async () => {
    setup = await setupWithAuto();
  });

  async function gotoWithAuto(page, context) {
    await context.addCookies(setup.state.cookies);
    await page.goto('/app.html');
    await page.selectOption('#autoSelector', String(setup.autoId));
  }

  test('GPS denied — location field empty and editable', async ({ page, context }) => {
    // Deny geolocation
    await context.grantPermissions([], { origin: BASE });
    await context.setGeolocation(null);

    await gotoWithAuto(page, context);
    await page.click('#addFillupBtn');
    await expect(page.locator('#fillupModal.open')).toBeVisible();

    // Location field should be empty and editable
    const locationField = page.locator('#fillupLocation');
    await expect(locationField).toBeEditable();
    // After a short wait GPS status should resolve to unavailable
    await page.waitForTimeout(500);
    const val = await locationField.inputValue();
    expect(val).toBe('');
  });

  test('saving new fillup appends to top of log', async ({ page, context }) => {
    await gotoWithAuto(page, context);
    const rowsBefore = await page.locator('tbody tr').count();

    await page.click('#addFillupBtn');
    await expect(page.locator('#fillupModal.open')).toBeVisible();

    // Fill required fields
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
    // Newest first — first row should have today's date
    await expect(page.locator('tbody tr').first()).toContainText('3/10/2026');
  });

  test('editing a fillup updates the row in place', async ({ page, context }) => {
    await gotoWithAuto(page, context);

    // Ensure at least one row
    await expect(page.locator('tbody tr')).toHaveCount(1);

    // Click Edit on first row
    await page.locator('tbody tr').first().getByRole('button', { name: 'Edit' }).click();
    await expect(page.locator('#fillupModal.open')).toBeVisible();
    expect(await page.locator('#fillupId').inputValue()).not.toBe('');

    // Change gallons
    await page.fill('#fillupGallons', '15.0');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/fillups/') && r.request().method() === 'PUT'),
      page.click('#fillupForm button[type="submit"]'),
    ]);

    // Row should reflect updated gallons
    await expect(page.locator('tbody tr').first()).toContainText('15.000');
  });

  test('client-side validation prevents submit with blank required fields', async ({ page, context }) => {
    await gotoWithAuto(page, context);
    await page.click('#addFillupBtn');
    await expect(page.locator('#fillupModal.open')).toBeVisible();

    // Clear required fields
    await page.fill('#fillupPrice', '');
    await page.fill('#fillupGallons', '');
    await page.fill('#fillupOdometer', '');

    // Click submit — browser validation should prevent it
    await page.click('#fillupForm button[type="submit"]');

    // Modal should still be open (form didn't submit)
    await expect(page.locator('#fillupModal.open')).toBeVisible();
  });
});
