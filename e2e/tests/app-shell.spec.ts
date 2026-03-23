import { test, expect, request } from '@playwright/test';
import { devLogin, uniqueEmail } from '../helpers/auth';

const BASE = process.env.BASE_URL ?? 'http://localhost:5100';

test.describe('App shell & autos management', () => {
  let cookieState: Awaited<ReturnType<typeof devLogin>>;

  test.beforeAll(async () => {
    const api = await request.newContext({ baseURL: BASE });
    cookieState = await devLogin(api, uniqueEmail('shell'));
    await api.dispose();
  });

  test('unauthenticated visit to /app.html redirects to login', async ({ page }) => {
    await page.context().clearCookies();
    await page.goto('/app.html');
    await page.waitForURL('**/');
    expect(page.url()).toMatch(/\/$/);
  });

  test('add auto appears in list and selector', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');
    await page.click('.tab-btn[data-tab="autos"]');
    await page.click('#addAutoBtn');
    await page.fill('#autoBrand', 'Honda');
    await page.fill('#autoModel', 'Civic');
    await page.fill('#autoPlate', 'SHL-001');
    await page.fill('#autoOdometer', '30000');
    await page.click('#autoForm button[type="submit"]');
    await expect(page.locator('.auto-card')).toContainText('Honda Civic');
    await expect(page.locator('#autoSelector')).toContainText('Honda Civic');
  });

  test('edit auto updates the card', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');
    await page.click('.tab-btn[data-tab="autos"]');
    await page.click('#addAutoBtn');
    await page.fill('#autoBrand', 'Ford');
    await page.fill('#autoModel', 'Focus');
    await page.fill('#autoPlate', 'SHL-EDIT');
    await page.fill('#autoOdometer', '45000');
    await page.click('#autoForm button[type="submit"]');
    await expect(page.locator('.auto-card', { hasText: 'Ford Focus' })).toBeVisible();

    const card = page.locator('.auto-card', { hasText: 'Ford Focus' });
    await card.getByRole('button', { name: 'Edit' }).click();
    await page.waitForSelector('#autoModal.open');
    await page.fill('#autoModel', 'Mustang');
    await Promise.all([
      page.waitForResponse(r => r.url().includes('/api/autos') && r.request().method() === 'PUT'),
      page.click('#autoForm button[type="submit"]'),
    ]);
    await expect(page.locator('.auto-card', { hasText: 'Ford Mustang' })).toBeVisible();
  });

  test('delete auto removes it from list and selector', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');
    await page.click('.tab-btn[data-tab="autos"]');
    await page.click('#addAutoBtn');
    await page.fill('#autoBrand', 'Chevy');
    await page.fill('#autoModel', 'Malibu');
    await page.fill('#autoPlate', 'SHL-DEL');
    await page.fill('#autoOdometer', '60000');
    await page.click('#autoForm button[type="submit"]');
    await expect(page.locator('.auto-card', { hasText: 'Chevy Malibu' })).toBeVisible();

    page.on('dialog', d => d.accept());
    await page.locator('.auto-card', { hasText: 'Chevy Malibu' }).locator('text=Delete').click();
    await expect(page.locator('.auto-card', { hasText: 'Chevy Malibu' })).not.toBeVisible();
    const selectorText = await page.locator('#autoSelector').textContent();
    expect(selectorText).not.toContain('Chevy Malibu');
  });

  test('logout clears session and redirects to login', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');
    await page.click('#logoutBtn');
    await page.waitForURL('**/');
    expect(page.url()).toMatch(/\/$/);
  });
});
