/**
 * UTC storage / local display tests.
 *
 * All fillup datetimes are stored as UTC in the database and must be
 * displayed in the browser's local timezone. The browser is forced to
 * America/New_York (UTC-5 in winter) so offsets are deterministic.
 *
 * Reference fillup: 2026-01-15T20:00:00Z  →  3:00 PM EST
 */

import { test, expect, request } from '@playwright/test';
import { devLogin, uniqueEmail, cleanupUser } from '../helpers/auth';

test.use({ timezoneId: 'America/New_York' });

const BASE = process.env.BASE_URL ?? 'http://localhost:5100';

// UTC time chosen so that local (EST = UTC-5) is visibly different:
// 2026-01-15T20:00:00Z  →  2026-01-15 3:00 PM EST
const UTC_FILLUP_TIME = '2026-01-15T20:00:00Z';
// What the datetime-local input must show (local time, no Z)
const LOCAL_INPUT_VALUE = '2026-01-15T15:00';

test.describe('UTC storage / local time display', () => {
  let cookieState: Awaited<ReturnType<typeof devLogin>>;
  let autoId: number;
  let fillupId: number;
  let email: string;

  test.beforeAll(async () => {
    const api = await request.newContext({ baseURL: BASE });
    email = uniqueEmail('tz');
    cookieState = await devLogin(api, email);

    const autoRes = await api.post('/api/autos', {
      data: { brand: 'TZTest', model: 'Car', plate: 'TZ-001', odometer: 10000 },
    });
    autoId = (await autoRes.json()).id;

    const fillupRes = await api.post(`/api/autos/${autoId}/fillups`, {
      data: {
        filledAt: UTC_FILLUP_TIME,
        fuelType: 0,
        pricePerGallon: 3.50,
        gallons: 10,
        odometer: 10300,
        isPartialFill: false,
      },
    });
    fillupId = (await fillupRes.json()).id;

    await api.dispose();
  });

  test.afterAll(async () => {
    const api = await request.newContext({ baseURL: BASE });
    await cleanupUser(api, email);
    await api.dispose();
  });

  test('fillup log displays UTC time converted to local time', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/log');
    await page.selectOption('#autoSelector', String(autoId));

    const row = page.locator('tbody tr').first();
    await expect(row).toBeVisible();

    // Must show 3:00 PM local, NOT 8:00 PM UTC
    await expect(row).toContainText('3:00 PM');
    await expect(row).not.toContainText('8:00 PM');
  });

  test('edit modal pre-populates datetime-local with local time', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/log');
    await page.selectOption('#autoSelector', String(autoId));

    await page.locator('tbody tr').first().getByRole('button', { name: 'Edit' }).click();
    await expect(page.locator('[id="filledAt"]')).toBeVisible();

    const inputValue = await page.locator('[id="filledAt"]').inputValue();
    // datetime-local must reflect local time (15:00), not UTC (20:00)
    expect(inputValue).toBe(LOCAL_INPUT_VALUE);
  });

  test('saving edit round-trips UTC time unchanged', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/log');
    await page.selectOption('#autoSelector', String(autoId));

    // Intercept the PUT request to capture what the frontend sends
    let sentBody: any;
    page.on('request', req => {
      if (req.url().includes('/fillups/') && req.method() === 'PUT') {
        try { sentBody = JSON.parse(req.postData() ?? '{}'); } catch { /* ignore */ }
      }
    });

    await page.locator('tbody tr').first().getByRole('button', { name: 'Edit' }).click();
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/fillups/') && r.request().method() === 'PUT'),
      page.locator('button.btn-save').click(),
    ]);

    // The submitted filledAt must be the original UTC time (within same minute)
    expect(sentBody).not.toBeNull();
    const submittedDate = new Date(sentBody.filledAt);
    const originalDate = new Date(UTC_FILLUP_TIME);
    expect(submittedDate.getUTCHours()).toBe(originalDate.getUTCHours());
    expect(submittedDate.getUTCMinutes()).toBe(originalDate.getUTCMinutes());
    expect(submittedDate.getUTCDate()).toBe(originalDate.getUTCDate());
  });

  test('new fillup modal defaults to local time (not UTC)', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/log');
    await page.selectOption('#autoSelector', String(autoId));

    // Record local time just before opening the modal
    const localNow = await page.evaluate(() => {
      const d = new Date();
      const pad = (n: number) => n.toString().padStart(2, '0');
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    });

    await page.locator('button.btn-add').click();
    await expect(page.locator('[id="filledAt"]')).toBeVisible();

    const inputValue = await page.locator('[id="filledAt"]').inputValue();
    // Default must match local time, not UTC — they differ by 5h in EST
    expect(inputValue).toBe(localNow);
  });
});
