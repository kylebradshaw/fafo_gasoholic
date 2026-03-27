import { test, expect, request } from '@playwright/test';
import { devLogin, uniqueEmail } from '../helpers/auth';

const BASE = process.env.BASE_URL ?? 'http://localhost:5100';

test.describe('PWA features', () => {
  let cookieState: Awaited<ReturnType<typeof devLogin>>;

  test.beforeAll(async () => {
    const api = await request.newContext({ baseURL: BASE });
    cookieState = await devLogin(api, uniqueEmail('pwa'));
    await api.dispose();
  });

  test('service worker is registered', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/log');

    // Check if service worker is registered
    const swRegistrations = await page.evaluate(() => {
      return navigator.serviceWorker.getRegistrations().then(regs => regs.length > 0);
    });
    expect(swRegistrations).toBe(true);
  });

  test('manifest.webmanifest is served with correct PWA metadata', async ({ request: req }) => {
    const res = await req.get('/manifest.webmanifest');
    expect(res.status()).toBe(200);
    const manifest = await res.json();
    expect(manifest.name).toBeTruthy();
    expect(manifest.short_name).toBeTruthy();
    expect(manifest.start_url).toBeTruthy();
    expect(manifest.display).toBe('standalone');
    expect(manifest.theme_color).toBeTruthy();
    expect(manifest.background_color).toBeTruthy();
    expect(manifest.icons).toBeTruthy();
    expect(Array.isArray(manifest.icons)).toBe(true);
  });

  test('offline fallback renders app shell', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/log');

    // Go offline
    await context.setOffline(true);

    // Navigate to a new route — should still show app shell (cached)
    await page.goto('/app/autos');
    await page.waitForLoadState('load');

    // The page should still be visible (not a network error)
    expect(page.url()).toContain('/app/autos');
    const content = await page.content();
    expect(content).toBeTruthy();

    // Go back online
    await context.setOffline(false);
  });

  test('PWA is installable (has minimal PWA criteria)', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app/log');

    // Check for required PWA metadata in the page
    const manifest = await page.locator('link[rel="manifest"]').getAttribute('href');
    expect(manifest).toBeTruthy();

    const themeColor = await page.locator('meta[name="theme-color"]').getAttribute('content');
    expect(themeColor).toBeTruthy();

    // Service worker should be registered
    const swRegistered = await page.evaluate(() => {
      return navigator.serviceWorker.getRegistrations().then(regs => regs.length > 0);
    });
    expect(swRegistered).toBe(true);
  });
});
