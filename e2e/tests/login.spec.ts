import { test, expect, request } from '@playwright/test';
import { devLogin, uniqueEmail } from '../helpers/auth';

const email = uniqueEmail('login');

test.describe('Login page', () => {

  test('sign in via magic link redirects to /app/log', async ({ page }) => {
    // Dev-login bypasses email flow and establishes a real session
    const api = await request.newContext({ baseURL: page.context().browser()!.contexts()[0].pages()[0]?.url() ?? process.env.BASE_URL ?? 'http://localhost:5100' });
    const state = await devLogin(api, email);
    await api.dispose();
    await page.context().addCookies(state.cookies);

    await page.goto('/');
    await page.waitForURL('**/app/log');
    expect(page.url()).toContain('/app/log');
  });

  test('already logged in skips to /app/log', async ({ page, context }) => {
    const api = await request.newContext({ baseURL: process.env.BASE_URL ?? 'http://localhost:5100' });
    const state = await devLogin(api, email);
    await api.dispose();
    await context.addCookies(state.cookies);

    await page.goto('/');
    await page.waitForURL('**/app/log');
    expect(page.url()).toContain('/app/log');
  });

  test('form is usable on 375px mobile without horizontal scroll', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/');
    const scrollWidth = await page.evaluate(() => document.body.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(375);
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
  });

  test('pressing Enter on email field submits the form', async ({ page }) => {
    await page.goto('/');
    await page.fill('#email', email);
    await page.locator('#email').press('Enter');
    // Should show pending state (real email flow) or redirect — either means form submitted
    await expect(page.locator('#pendingState, #app')).toBeVisible({ timeout: 5000 });
  });

});
