// @ts-check
const { defineConfig, devices } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  timeout: 30000,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5100',
    trace: 'off',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], headless: true },
    },
  ],
  webServer: {
    command: 'dotnet run --no-launch-profile --urls http://localhost:5100',
    cwd: '..',
    url: 'http://localhost:5100/health',
    reuseExistingServer: true,
    timeout: 30000,
  },
});
