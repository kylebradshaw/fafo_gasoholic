// @ts-check
const { test, expect, request } = require('@playwright/test');

const BASE = 'http://localhost:5100';
const TEST_EMAIL = `task6-${Date.now()}@test.com`;

test.describe('Task 6 — Login Page', () => {
  test('sign in redirects to /app.html', async ({ page }) => {
    await page.goto('/');
    await page.fill('#email', TEST_EMAIL);
    await page.click('button[type="submit"]');
    await page.waitForURL('**/app.html');
    expect(page.url()).toContain('/app.html');
  });

  test('already logged in redirects immediately', async ({ page, context }) => {
    // Login via API to set cookie
    const api = await request.newContext({ baseURL: BASE });
    const loginRes = await api.post('/auth/login', { data: { email: TEST_EMAIL } });
    expect(loginRes.ok()).toBeTruthy();

    // Copy cookies into page context
    const cookies = await api.storageState();
    await context.addCookies(cookies.cookies);

    await page.goto('/');
    await page.waitForURL('**/app.html');
    expect(page.url()).toContain('/app.html');
  });

  test('form usable on 375px mobile', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/');

    // No horizontal scroll
    const scrollWidth = await page.evaluate(() => document.body.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(375);

    // Form elements are visible
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });
});
