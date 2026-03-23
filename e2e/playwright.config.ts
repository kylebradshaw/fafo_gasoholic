import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.BASE_URL ?? 'http://localhost:5100';
const isLocal = !process.env.BASE_URL;

export default defineConfig({
  testDir: '.',
  fullyParallel: false,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL,
    extraHTTPHeaders: { 'Content-Type': 'application/json' },
    trace: 'retain-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], headless: true },
    },
  ],

  // Only spin up the local server when not targeting a remote URL
  webServer: isLocal ? {
    command: 'dotnet run --no-launch-profile --urls http://localhost:5100',
    cwd: '..',
    url: 'http://localhost:5100/health',
    reuseExistingServer: true,
    timeout: 30000,
  } : undefined,
});
