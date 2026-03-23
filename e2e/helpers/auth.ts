import { APIRequestContext, expect } from '@playwright/test';

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:5100';

/**
 * Creates a verified session via the dev-login endpoint.
 * Requires SMOKE_TEST_SECRET to be set (same secret used for smoke tests).
 *
 * Retrieve it from Key Vault:
 *   az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv
 */
export async function devLogin(api: APIRequestContext, email: string) {
  const secret = process.env.SMOKE_TEST_SECRET ?? '';
  if (!secret) {
    throw new Error(
      'SMOKE_TEST_SECRET must be set to run tests.\n' +
      'Get it from Key Vault: az keyvault secret show --vault-name gasoholic-kv --name SmokeTestSecret --query value -o tsv'
    );
  }
  const res = await api.post('/auth/dev-login', {
    headers: { 'X-Smoke-Test-Secret': secret },
    data: { email },
  });
  expect(res.status(), `devLogin failed for ${email} — wrong secret or endpoint not available`).toBe(200);
  return api.storageState();
}

export function uniqueEmail(prefix: string) {
  return `${prefix}-${Date.now()}@test.com`;
}
