// @ts-check
const { test, expect, request } = require('@playwright/test');

const BASE = 'http://localhost:5100';

async function loginAs(apiContext, email) {
  const res = await apiContext.post('/auth/login', { data: { email } });
  expect(res.ok()).toBeTruthy();
  return apiContext.storageState();
}

test.describe('Task 7 — App Shell & Autos Management', () => {
  let cookieState;
  const email = `task7-${Date.now()}@test.com`;

  test.beforeAll(async () => {
    const api = await request.newContext({ baseURL: BASE });
    cookieState = await loginAs(api, email);
    await api.dispose();
  });

  test('unauthenticated visit redirects to login', async ({ page }) => {
    // Clear cookies
    await page.context().clearCookies();
    await page.goto('/app.html');
    await page.waitForURL('**/');
    expect(page.url()).toMatch(/\//);
  });

  test('add auto appears in list and selector', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');

    // Go to Autos tab
    await page.click('text=Autos');
    await page.click('#addAutoBtn');

    await page.fill('#autoBrand', 'Honda');
    await page.fill('#autoModel', 'Civic');
    await page.fill('#autoPlate', 'T7-CIVIC');
    await page.fill('#autoOdometer', '30000');
    await page.click('#autoForm button[type="submit"]');

    // Card should appear
    await expect(page.locator('.auto-card')).toContainText('Honda Civic');

    // Selector should include it
    await expect(page.locator('#autoSelector')).toContainText('Honda Civic');
  });

  test('edit auto updates the card', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');

    // Add auto first
    await page.click('text=Autos');
    await page.click('#addAutoBtn');
    await page.fill('#autoBrand', 'Ford');
    await page.fill('#autoModel', 'Focus');
    await page.fill('#autoPlate', 'T7-EDIT');
    await page.fill('#autoOdometer', '45000');
    await page.click('#autoForm button[type="submit"]');
    await expect(page.locator('.auto-card', { hasText: 'Ford Focus' })).toBeVisible();

    // Edit it
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

    // Add auto to delete
    await page.click('text=Autos');
    await page.click('#addAutoBtn');
    await page.fill('#autoBrand', 'Chevy');
    await page.fill('#autoModel', 'Malibu');
    await page.fill('#autoPlate', 'T7-DEL');
    await page.fill('#autoOdometer', '60000');
    await page.click('#autoForm button[type="submit"]');
    await expect(page.locator('.auto-card', { hasText: 'Chevy Malibu' })).toBeVisible();

    // Delete it (handle confirm dialog)
    page.on('dialog', d => d.accept());
    const card = page.locator('.auto-card', { hasText: 'Chevy Malibu' });
    await card.locator('text=Delete').click();

    await expect(page.locator('.auto-card', { hasText: 'Chevy Malibu' })).not.toBeVisible();
    // Should not be in selector either
    const selectorText = await page.locator('#autoSelector').textContent();
    expect(selectorText).not.toContain('Chevy Malibu');
  });

  test('logout redirects to login', async ({ page, context }) => {
    await context.addCookies(cookieState.cookies);
    await page.goto('/app.html');
    await page.click('#logoutBtn');
    await page.waitForURL('**/');
    expect(page.url()).toMatch(/\//);
  });
});
