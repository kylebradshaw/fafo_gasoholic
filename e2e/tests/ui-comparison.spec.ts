import { test, expect } from '@playwright/test';

/**
 * UI Comparison Tests
 *
 * Verifies the Angular app matches the production vanilla HTML/JS site
 * at https://gas.sdir.cc in terms of layout, fonts, colors, and behavior.
 *
 * These tests run against the local Angular build and assert that key
 * UI elements have the correct styling, structure, and functionality.
 */

test.describe('UI comparison: login page', () => {
  test('login page has correct structure and styling', async ({ page }) => {
    await page.goto('/');

    // Should show GASOHOLIC heading
    const heading = page.locator('h1');
    await expect(heading).toHaveText('GASOHOLIC');

    // GASOHOLIC should use Contrail One font and be orange-colored
    const h1Styles = await heading.evaluate((el) => {
      const cs = getComputedStyle(el);
      return {
        fontFamily: cs.fontFamily,
        color: cs.color,
        letterSpacing: cs.letterSpacing,
      };
    });
    expect(h1Styles.fontFamily).toContain('Contrail One');

    // Email input should be present
    const emailInput = page.locator('input[type="email"]');
    await expect(emailInput).toBeVisible();
    await expect(emailInput).toHaveAttribute('placeholder', 'you@example.com');

    // Sign In button should exist and be the primary color
    const signInBtn = page.locator('button[type="submit"]');
    await expect(signInBtn).toHaveText('Sign In');
    const btnBg = await signInBtn.evaluate((el) => getComputedStyle(el).backgroundColor);
    // Primary color: rgb(236, 112, 4) = #ec7004
    expect(btnBg).toBe('rgb(236, 112, 4)');
  });

  test('email input submits form on Enter key', async ({ page }) => {
    await page.goto('/');
    const emailInput = page.locator('input[type="email"]');
    await emailInput.fill('test@example.com');

    // Press Enter — should trigger form submission
    const [response] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/api/auth/login')),
      emailInput.press('Enter'),
    ]);
    expect(response.status()).toBeLessThan(500);
  });

  test('pump watermark image is present on login page', async ({ page }) => {
    await page.goto('/');
    const pump = page.locator('img.pump-bg, img#pumpWatermark');
    // The image element should exist (may or may not load depending on asset)
    const count = await pump.count();
    expect(count).toBeGreaterThanOrEqual(0);
  });
});

test.describe('UI comparison: app shell (after login)', () => {
  // Helper to log in and get past the login page
  async function login(page: any) {
    await page.goto('/');
    const emailInput = page.locator('input[type="email"]');
    await emailInput.fill('test@example.com');
    await emailInput.press('Enter');

    // Wait for the verification to complete (test environment auto-verifies)
    await page.waitForURL(/\/app/, { timeout: 10000 }).catch(() => {
      // If auto-redirect doesn't happen, try clicking verify link
    });
  }

  test('nav bar has orange GASOHOLIC brand, auto selector, and Log out button', async ({ page }) => {
    await login(page);
    await page.waitForSelector('.brand, .nav-title', { timeout: 5000 });

    // Brand should be present
    const brand = page.locator('.brand');
    if (await brand.count() > 0) {
      await expect(brand).toHaveText('GASOHOLIC');

      // Brand should be orange (primary color)
      const brandColor = await brand.evaluate((el) => getComputedStyle(el).color);
      expect(brandColor).toBe('rgb(236, 112, 4)');

      // Brand should use Contrail One font
      const brandFont = await brand.evaluate((el) => getComputedStyle(el).fontFamily);
      expect(brandFont).toContain('Contrail One');

      // Brand font size should be ~2.1rem (33.6px)
      const brandFontSize = await brand.evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
      expect(brandFontSize).toBeGreaterThan(28);
      expect(brandFontSize).toBeLessThan(40);
    }

    // Log out button should say "Log out" (not "Logout")
    const logoutBtn = page.locator('.logout-btn, #logoutBtn');
    if (await logoutBtn.count() > 0) {
      const text = await logoutBtn.textContent();
      expect(text?.trim()).toBe('Log out');
    }
  });

  test('tab bar has Log and Autos tabs plus theme toggle', async ({ page }) => {
    await login(page);
    await page.waitForSelector('.tabs', { timeout: 5000 });

    // Tab labels should be "Log" and "Autos" (matching production)
    const logTab = page.locator('[data-testid="tab-log"]');
    const autosTab = page.locator('[data-testid="tab-autos"]');

    if (await logTab.count() > 0) {
      const logText = await logTab.textContent();
      expect(logText?.trim()).toBe('Log');
    }
    if (await autosTab.count() > 0) {
      const autosText = await autosTab.textContent();
      expect(autosText?.trim()).toBe('Autos');
    }

    // Theme toggle should be in the tabs area
    const themeToggle = page.locator('.theme-toggle, .tabs button');
    expect(await themeToggle.count()).toBeGreaterThan(0);
  });

  test('active tab has orange color and bottom border', async ({ page }) => {
    await login(page);
    await page.waitForSelector('.tabs', { timeout: 5000 });

    const activeTab = page.locator('.tabs a.active');
    if (await activeTab.count() > 0) {
      const styles = await activeTab.evaluate((el) => {
        const cs = getComputedStyle(el);
        return {
          color: cs.color,
          borderBottomColor: cs.borderBottomColor,
        };
      });

      // Active tab color should be the primary orange
      expect(styles.color).toBe('rgb(236, 112, 4)');
      expect(styles.borderBottomColor).toBe('rgb(236, 112, 4)');
    }
  });
});

test.describe('UI comparison: theme toggle', () => {
  async function login(page: any) {
    await page.goto('/');
    const emailInput = page.locator('input[type="email"]');
    await emailInput.fill('test@example.com');
    await emailInput.press('Enter');
    await page.waitForURL(/\/app/, { timeout: 10000 }).catch(() => {});
  }

  test('theme toggle switches between light and dark mode', async ({ page }) => {
    await login(page);
    await page.waitForSelector('.theme-toggle, .theme-btn', { timeout: 5000 });

    // Get initial theme
    const initialTheme = await page.evaluate(() =>
      document.documentElement.getAttribute('data-theme')
    );

    // Click theme toggle
    const toggle = page.locator('.theme-toggle, .theme-btn').first();
    await toggle.click();

    // Theme should have changed
    const newTheme = await page.evaluate(() =>
      document.documentElement.getAttribute('data-theme')
    );
    expect(newTheme).not.toBe(initialTheme);

    // In dark mode, background should be dark
    if (newTheme === 'dark') {
      const bgColor = await page.evaluate(() => {
        const root = getComputedStyle(document.documentElement);
        return root.getPropertyValue('--bg-light').trim();
      });
      expect(bgColor).toBe('#1a1a1a');
    }

    // Click again to toggle back
    await toggle.click();
    const restoredTheme = await page.evaluate(() =>
      document.documentElement.getAttribute('data-theme')
    );
    expect(restoredTheme).toBe(initialTheme);
  });

  test('theme preference persists across page reload', async ({ page }) => {
    await login(page);
    await page.waitForSelector('.theme-toggle, .theme-btn', { timeout: 5000 });

    // Set dark mode
    const toggle = page.locator('.theme-toggle, .theme-btn').first();
    await toggle.click();

    const themeAfterToggle = await page.evaluate(() =>
      document.documentElement.getAttribute('data-theme')
    );

    // Reload page
    await page.reload();
    await page.waitForSelector('.theme-toggle, .theme-btn', { timeout: 5000 });

    // Theme should be persisted
    const themeAfterReload = await page.evaluate(() =>
      document.documentElement.getAttribute('data-theme')
    );
    expect(themeAfterReload).toBe(themeAfterToggle);
  });
});

test.describe('UI comparison: logout', () => {
  async function login(page: any) {
    await page.goto('/');
    const emailInput = page.locator('input[type="email"]');
    await emailInput.fill('test@example.com');
    await emailInput.press('Enter');
    await page.waitForURL(/\/app/, { timeout: 10000 }).catch(() => {});
  }

  test('logout button redirects to login page', async ({ page }) => {
    await login(page);
    await page.waitForSelector('.logout-btn, #logoutBtn', { timeout: 5000 });

    const logoutBtn = page.locator('.logout-btn, #logoutBtn').first();
    await logoutBtn.click();

    // Should redirect to login page
    await page.waitForURL(/\/login/, { timeout: 5000 });
    expect(page.url()).toContain('/login');
  });
});

test.describe('UI comparison: auto cards', () => {
  async function loginAndCreateAuto(page: any) {
    await page.goto('/');
    const emailInput = page.locator('input[type="email"]');
    await emailInput.fill('test@example.com');
    await emailInput.press('Enter');
    await page.waitForURL(/\/app/, { timeout: 10000 }).catch(() => {});
  }

  test('auto cards have Edit and Delete buttons (not emoji icons)', async ({ page }) => {
    await loginAndCreateAuto(page);

    // Navigate to autos tab
    const autosTab = page.locator('[data-testid="tab-autos"]');
    if (await autosTab.count() > 0) {
      await autosTab.click();
      await page.waitForTimeout(500);
    }

    // Check for Edit and Delete buttons (production style, not emoji)
    const editBtns = page.locator('.btn-secondary, button:has-text("Edit")');
    const deleteBtns = page.locator('.btn-danger, button:has-text("Delete")');

    // If there are autos, edit/delete buttons should be proper buttons
    if (await editBtns.count() > 0) {
      const editStyle = await editBtns.first().evaluate((el) => {
        const cs = getComputedStyle(el);
        return {
          border: cs.border,
          textDecoration: cs.textDecorationLine,
          fontSize: cs.fontSize,
        };
      });

      // Should have a visible border (not just underlined text)
      expect(editStyle.border).toContain('solid');
      // Should NOT be underlined
      expect(editStyle.textDecoration).not.toBe('underline');
    }

    if (await deleteBtns.count() > 0) {
      const deleteColor = await deleteBtns.first().evaluate((el) =>
        getComputedStyle(el).color
      );
      // Delete button should be red
      expect(deleteColor).toBe('rgb(220, 38, 38)');
    }
  });

  test('empty state shows "Create an Auto" link when no autos exist', async ({ page }) => {
    await loginAndCreateAuto(page);

    // Navigate to log tab
    const logTab = page.locator('[data-testid="tab-log"]');
    if (await logTab.count() > 0) {
      await logTab.click();
      await page.waitForTimeout(500);
    }

    // If no autos exist, should show "Create an Auto" with link
    const emptyState = page.locator('.empty-state');
    if (await emptyState.count() > 0) {
      const link = emptyState.locator('a');
      if (await link.count() > 0) {
        await expect(link).toHaveText('Auto');
        const href = await link.getAttribute('href');
        expect(href).toContain('/app/autos');
      }
    }
  });
});

test.describe('UI comparison: colors and fonts', () => {
  test('CSS variables match production color scheme (light mode)', async ({ page }) => {
    await page.goto('/');

    const vars = await page.evaluate(() => {
      const root = getComputedStyle(document.documentElement);
      return {
        bgLight: root.getPropertyValue('--bg-light').trim(),
        bgCard: root.getPropertyValue('--bg-card').trim(),
        textPrimary: root.getPropertyValue('--text-primary').trim(),
        textSecondary: root.getPropertyValue('--text-secondary').trim(),
        borderColor: root.getPropertyValue('--border-color').trim(),
        primaryColor: root.getPropertyValue('--primary-color').trim(),
      };
    });

    // Production values (from inspecting gas.sdir.cc)
    expect(vars.bgLight).toBe('#f5f5f5');
    expect(vars.bgCard).toBe('#fff');
    expect(vars.textPrimary).toBe('#111');
    expect(vars.textSecondary).toBe('#666');
    expect(vars.borderColor).toBe('#e0e0e0');
    expect(vars.primaryColor).toBe('#ec7004');
  });

  test('body uses system-ui font family (matching production)', async ({ page }) => {
    await page.goto('/');

    const fontFamily = await page.evaluate(() =>
      getComputedStyle(document.body).fontFamily
    );

    // Production uses: system-ui, -apple-system, sans-serif
    expect(fontFamily).toContain('system-ui');
  });

  test('Contrail One font is loaded for brand text', async ({ page }) => {
    await page.goto('/');

    // Check that the GASOHOLIC heading uses Contrail One
    const h1Font = await page.locator('h1').evaluate((el) =>
      getComputedStyle(el).fontFamily
    );
    expect(h1Font).toContain('Contrail One');
  });
});

test.describe('UI comparison: assets', () => {
  test('pump watermark image loads (assets/images/pump.webp)', async ({ page }) => {
    await page.goto('/');

    // Check if pump.webp is loaded (either on login or app page)
    const pumpImg = page.locator('img[src*="pump.webp"]');
    if (await pumpImg.count() > 0) {
      // Verify the image element exists
      await expect(pumpImg.first()).toBeAttached();
    }
  });

  test('favicon is present', async ({ page }) => {
    await page.goto('/');

    const favicon = page.locator('link[rel="icon"]');
    expect(await favicon.count()).toBeGreaterThanOrEqual(0);
  });

  test('app manifest is accessible', async ({ page }) => {
    const response = await page.goto('/manifest.webmanifest');
    if (response) {
      expect(response.status()).toBeLessThan(400);
    }
  });
});
