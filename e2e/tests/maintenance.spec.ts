import { test, expect, request } from '@playwright/test';
import { devLogin, uniqueEmail } from '../helpers/auth';

const BASE = process.env.BASE_URL ?? 'http://localhost:5100';

test.describe('Maintenance log tab', () => {
  let cookieState: Awaited<ReturnType<typeof devLogin>>;
  let autoId: number;

  test.beforeAll(async () => {
    const api = await request.newContext({ baseURL: BASE });
    cookieState = await devLogin(api, uniqueEmail('maint'));

    const autoRes = await api.post('/api/autos', {
      data: { brand: 'MaintTest', model: 'Truck', plate: 'MNT-001', odometer: 50000 },
    });
    autoId = (await autoRes.json()).id;

    // Seed two maintenance records via API
    await api.post(`/api/autos/${autoId}/maintenance`, { data: {
      type: 'OilChange',
      performedAt: '2026-03-01T00:00:00Z',
      odometer: 50500,
      cost: 45.99,
      notes: 'Synthetic blend',
    }});
    await api.post(`/api/autos/${autoId}/maintenance`, { data: {
      type: 'TireRotation',
      performedAt: '2026-03-10T00:00:00Z',
      odometer: 51200,
      cost: 25.00,
      notes: null,
    }});

    await api.dispose();
  });

  async function gotoMaintenance(page: import('@playwright/test').Page, context: import('@playwright/test').BrowserContext) {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/maintenance');
    await page.selectOption('#autoSelector', String(autoId));
  }

  test('shows empty state when no auto is selected', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/maintenance');
    await page.selectOption('#autoSelector', '');
    await expect(page.locator('.empty-state')).toContainText('Select a vehicle');
  });

  test('table renders seeded records', async ({ page, context }) => {
    await gotoMaintenance(page, context);
    const rows = page.locator('tbody tr');
    await expect(rows).toHaveCount(2);
    // Records ordered by odometer desc — TireRotation (51200) first
    await expect(rows.first()).toContainText('Tire Rotation');
    await expect(rows.nth(1)).toContainText('Oil Change');
  });

  test('add maintenance record via modal', async ({ page, context }) => {
    await gotoMaintenance(page, context);
    await expect(page.locator('tbody tr')).toHaveCount(2);

    await page.click('button:has-text("+ Add Record")');
    await expect(page.locator('.modal-backdrop')).toBeVisible();
    await expect(page.locator('.modal-header h2')).toHaveText('Add Maintenance Record');

    await page.selectOption('#type', 'BrakeInspection');
    await page.fill('#performedAt', '2026-03-15');
    await page.fill('#odometer', '51800');
    await page.fill('#cost', '120.50');
    await page.fill('#notes', 'Front and rear pads checked');

    await Promise.all([
      page.waitForResponse(r => r.url().includes('/maintenance') && r.request().method() === 'POST'),
      page.click('button.btn-save'),
    ]);

    // Modal should close
    await expect(page.locator('.modal-backdrop')).not.toBeVisible();
    // Table should now have 3 rows
    await expect(page.locator('tbody tr')).toHaveCount(3);
    await expect(page.locator('tbody tr').first()).toContainText('Brake Inspection');
  });

  test('edit maintenance record via modal', async ({ page, context }) => {
    await gotoMaintenance(page, context);

    // Click Edit on the first row
    await page.locator('tbody tr').first().getByRole('button', { name: 'Edit' }).click();
    await expect(page.locator('.modal-backdrop')).toBeVisible();
    await expect(page.locator('.modal-header h2')).toHaveText('Edit Maintenance Record');

    // Change cost
    await page.fill('#cost', '135.00');

    await Promise.all([
      page.waitForResponse(r => r.url().includes('/maintenance/') && r.request().method() === 'PUT'),
      page.click('button.btn-save'),
    ]);

    await expect(page.locator('.modal-backdrop')).not.toBeVisible();
    await expect(page.locator('tbody tr').first()).toContainText('135.00');
  });

  test('delete maintenance record removes row', async ({ page, context }) => {
    await gotoMaintenance(page, context);
    const countBefore = await page.locator('tbody tr').count();

    // Accept the confirm dialog
    page.on('dialog', dialog => dialog.accept());

    await page.locator('tbody tr').last().getByRole('button', { name: 'Del' }).click();
    await expect(page.locator('tbody tr')).toHaveCount(countBefore - 1);
  });

  test('modal cancel closes without saving', async ({ page, context }) => {
    await gotoMaintenance(page, context);
    const countBefore = await page.locator('tbody tr').count();

    await page.click('button:has-text("+ Add Record")');
    await expect(page.locator('.modal-backdrop')).toBeVisible();

    await page.click('button.btn-cancel');
    await expect(page.locator('.modal-backdrop')).not.toBeVisible();
    await expect(page.locator('tbody tr')).toHaveCount(countBefore);
  });

  test('save button disabled with empty required fields', async ({ page, context }) => {
    await gotoMaintenance(page, context);

    await page.click('button:has-text("+ Add Record")');
    await expect(page.locator('.modal-backdrop')).toBeVisible();

    // Form opens with empty type, odometer, cost — save should be disabled
    const saveBtn = page.locator('button.btn-save');
    await expect(saveBtn).toBeDisabled();
  });

  test('switching auto reloads maintenance records', async ({ page, context }) => {
    await gotoMaintenance(page, context);
    await expect(page.locator('tbody tr').first()).toBeVisible();

    // Deselect auto
    await page.selectOption('#autoSelector', '');
    await expect(page.locator('.empty-state')).toContainText('Select a vehicle');
  });

  test('table is scrollable on mobile viewport', async ({ page, context }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await gotoMaintenance(page, context);
    await expect(page.locator('.table-wrap')).toBeVisible();
    const bodyScroll = await page.evaluate(() => document.body.scrollWidth);
    expect(bodyScroll).toBeLessThanOrEqual(375);
  });
});
