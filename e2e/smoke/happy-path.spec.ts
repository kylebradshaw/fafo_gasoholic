import { test, expect, request as requestFactory } from '@playwright/test';
import { devLogin, uniqueEmail, cleanupUser } from '../helpers/auth';

/**
 * Smoke tests — full happy-path API walkthrough against a deployed environment.
 *
 * Run against production:
 *   BASE_URL=https://gas.sdir.cc SMOKE_TEST_SECRET=<secret> npm run test:smoke
 *
 * Run against local:
 *   SMOKE_TEST_SECRET=<secret> npm test
 *
 * The SMOKE_TEST_SECRET is required. Retrieve it from Key Vault:
 *   az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv
 */

const SMOKE_SECRET = process.env.SMOKE_TEST_SECRET ?? '';
const testEmail = `smoke-${Date.now()}@example.com`;

// State shared across steps within the test
let autoId: number;
let fillup1Id: number;
let fillup2Id: number;

test.describe('Happy path @smoke', () => {

  test.afterAll(async () => {
    if (!SMOKE_SECRET) return;
    // Clean up all test users created during this run
    const api = await requestFactory.newContext({
      baseURL: process.env.BASE_URL ?? 'http://localhost:5100',
    });
    await cleanupUser(api, testEmail);
    await api.dispose();
  });

  test('health check', { tag: ['@smoke'] }, async ({ request }) => {
    const res = await request.get('/health');
    expect(res.status(), 'GET /health should return 200').toBe(200);
    const body = await res.json();
    expect(body.status).toBe('ok');
  });

  test('unauthenticated /auth/me returns 401', { tag: ['@smoke'] }, async ({ request }) => {
    const res = await request.get('/auth/me');
    expect(res.status(), 'unauthenticated /auth/me should return 401').toBe(401);
  });

  test('dev login', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'SMOKE_TEST_SECRET not set — cannot authenticate without it');

    const res = await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });
    expect(res.status(), 'POST /auth/dev-login should return 200').toBe(200);
    const body = await res.json();
    expect(body.status).toBe('ok');
    expect(body.email).toBe(testEmail);
  });

  test('wrong secret is rejected', { tag: ['@smoke'] }, async ({ request }) => {
    const res = await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': 'wrong-secret' },
      data: { email: testEmail },
    });
    expect(res.status(), 'wrong secret should return 403').toBe(403);
  });

  test('authenticated /auth/me returns 200', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    // Re-establish session for this request context
    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });

    const res = await request.get('/auth/me');
    expect(res.status(), 'authenticated /auth/me should return 200').toBe(200);
    const body = await res.json();
    expect(body.email).toBe(testEmail);
  });

  test('create auto', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });

    const res = await request.post('/api/autos', {
      data: { brand: 'Toyota', model: 'Smoke Test', plate: 'TST001', odometer: 10000 },
    });
    expect(res.status(), 'POST /api/autos should return 201').toBe(201);
    const body = await res.json();
    expect(body.id).toBeTruthy();
    autoId = body.id;
  });

  test('auto appears in list', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });
    if (!autoId) {
      const r = await request.post('/api/autos', {
        data: { brand: 'Toyota', model: 'Smoke Test', plate: 'TST001', odometer: 10000 },
      });
      autoId = (await r.json()).id;
    }

    const res = await request.get('/api/autos');
    expect(res.status()).toBe(200);
    const autos = await res.json();
    expect(autos.some((a: { id: number }) => a.id === autoId), 'auto should appear in list').toBeTruthy();
  });

  test('add first fillup (base, no MPG expected)', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });
    if (!autoId) {
      const r = await request.post('/api/autos', {
        data: { brand: 'Toyota', model: 'Smoke Test', plate: 'TST001', odometer: 10000 },
      });
      autoId = (await r.json()).id;
    }

    const res = await request.post(`/api/autos/${autoId}/fillups`, {
      data: {
        filledAt: new Date().toISOString(),
        fuelType: 0,
        pricePerGallon: 3.50,
        gallons: 10.0,
        odometer: 10000,
        isPartialFill: false,
      },
    });
    expect(res.status(), 'POST first fillup should return 201').toBe(201);
    const body = await res.json();
    expect(body.id).toBeTruthy();
    fillup1Id = body.id;
  });

  test('add second fillup and verify MPG ~30', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });
    if (!autoId) {
      const r = await request.post('/api/autos', {
        data: { brand: 'Toyota', model: 'Smoke Test', plate: 'TST001', odometer: 10000 },
      });
      autoId = (await r.json()).id;
    }
    if (!fillup1Id) {
      const r = await request.post(`/api/autos/${autoId}/fillups`, {
        data: { filledAt: new Date().toISOString(), fuelType: 0, pricePerGallon: 3.50, gallons: 10.0, odometer: 10000, isPartialFill: false },
      });
      fillup1Id = (await r.json()).id;
    }

    const res = await request.post(`/api/autos/${autoId}/fillups`, {
      data: {
        filledAt: new Date().toISOString(),
        fuelType: 0,
        pricePerGallon: 3.60,
        gallons: 10.0,
        odometer: 10300,
        isPartialFill: false,
      },
    });
    expect(res.status(), 'POST second fillup should return 201').toBe(201);
    const body = await res.json();
    fillup2Id = body.id;

    // Verify MPG on the fillup list
    const listRes = await request.get(`/api/autos/${autoId}/fillups`);
    expect(listRes.status()).toBe(200);
    const fillups = await listRes.json();
    const second = fillups.find((f: { id: number }) => f.id === fillup2Id);
    expect(second, 'second fillup should appear in list').toBeTruthy();
    expect(second.mpg, 'MPG should be computed (non-null)').not.toBeNull();
    expect(second.mpg, 'MPG should be approximately 30').toBeCloseTo(30, 0);
  });

  test('edit auto', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });
    if (!autoId) {
      const r = await request.post('/api/autos', {
        data: { brand: 'Toyota', model: 'Smoke Test', plate: 'TST001', odometer: 10000 },
      });
      autoId = (await r.json()).id;
    }

    const res = await request.put(`/api/autos/${autoId}`, {
      data: { brand: 'Toyota', model: 'Smoke Test Updated', plate: 'TST001', odometer: 10300 },
    });
    expect(res.status(), 'PUT /api/autos/:id should return 200').toBe(200);
  });

  test('delete fillup', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });
    if (!autoId) {
      const r = await request.post('/api/autos', {
        data: { brand: 'Toyota', model: 'Smoke Test', plate: 'TST001', odometer: 10000 },
      });
      autoId = (await r.json()).id;
    }
    if (!fillup1Id) {
      const r = await request.post(`/api/autos/${autoId}/fillups`, {
        data: { filledAt: new Date().toISOString(), fuelType: 0, pricePerGallon: 3.50, gallons: 10.0, odometer: 10000, isPartialFill: false },
      });
      fillup1Id = (await r.json()).id;
    }

    const res = await request.delete(`/api/autos/${autoId}/fillups/${fillup1Id}`);
    expect(res.status(), 'DELETE fillup should return 204').toBe(204);
  });

  test('delete auto', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });
    if (!autoId) {
      const r = await request.post('/api/autos', {
        data: { brand: 'Toyota', model: 'Smoke Test', plate: 'TST001', odometer: 10000 },
      });
      autoId = (await r.json()).id;
    }

    const res = await request.delete(`/api/autos/${autoId}`);
    expect(res.status(), 'DELETE auto should return 204').toBe(204);
  });

  test('auto selector defaults to first auto when no fillups exist', { tag: ['@smoke'] }, async ({ page, context }) => {
    test.skip(!SMOKE_SECRET, 'SMOKE_TEST_SECRET not set — cannot authenticate without it');

    const BASE = process.env.BASE_URL ?? 'http://localhost:5100';
    const api = await requestFactory.newContext({ baseURL: BASE });
    const selectorEmail = uniqueEmail('selector-smoke');
    const state = await devLogin(api, selectorEmail);

    // Create an auto with no fillups
    await api.post('/api/autos', {
      data: { brand: 'Selector', model: 'Test', plate: 'SEL001', odometer: 1000 },
    });
    await cleanupUser(api, selectorEmail);
    await api.dispose();

    await context.addCookies(state.cookies);
    await page.goto('/app.html');

    // Selector must not show the placeholder — it must have a real auto selected
    const selectedValue = await page.locator('#autoSelector').inputValue();
    expect(selectedValue, 'auto selector should not be empty when autos exist').not.toBe('');

    // The log panel must not show the "select an auto" hint
    await expect(page.locator('#fillupContent')).not.toHaveText(/Select an auto/i);
  });

  test('logout clears session', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires dev login');

    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: testEmail },
    });

    const logout = await request.post('/auth/logout');
    expect(logout.status(), 'POST /auth/logout should return 200').toBe(200);

    const me = await request.get('/auth/me');
    expect(me.status(), '/auth/me after logout should return 401').toBe(401);
  });

  test('cleanup: reject non-test email domain', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires smoke secret');

    const res = await request.delete('/auth/dev-cleanup', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email: 'real-user@gmail.com' },
    });
    expect(res.status(), 'cleanup should reject non-test domains').toBe(400);
  });

  test('cleanup: wrong secret is rejected', { tag: ['@smoke'] }, async ({ request }) => {
    const res = await request.delete('/auth/dev-cleanup', {
      headers: { 'X-Smoke-Test-Secret': 'wrong-secret' },
      data: { email: 'test@example.com' },
    });
    expect(res.status(), 'cleanup with wrong secret should return 403').toBe(403);
  });

  test('cleanup: test user deleted after run', { tag: ['@smoke'] }, async ({ request }) => {
    test.skip(!SMOKE_SECRET, 'requires smoke secret');

    // Create a fresh user
    const email = `cleanup-verify-${Date.now()}@example.com`;
    await request.post('/auth/dev-login', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email },
    });

    // Delete it
    const del = await request.delete('/auth/dev-cleanup', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email },
    });
    expect(del.status(), 'DELETE /auth/dev-cleanup should return 204').toBe(204);

    // Second delete returns 404
    const del2 = await request.delete('/auth/dev-cleanup', {
      headers: { 'X-Smoke-Test-Secret': SMOKE_SECRET },
      data: { email },
    });
    expect(del2.status(), 'second cleanup should return 404').toBe(404);
  });

});
