import { defineConfig, devices } from '@playwright/test';

const vitePort = process.env.E2E_VITE_PORT ?? '5175';
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? `http://127.0.0.1:${vitePort}`;

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [['line']],
  timeout: 60_000,
  expect: {
    timeout: 15_000,
  },
  use: {
    baseURL,
    trace: 'off',
    screenshot: 'off',
    video: 'off',
  },
  webServer: {
    command: 'node e2e/start-stack.mjs',
    url: baseURL,
    reuseExistingServer: false,
    timeout: 240_000,
    stdout: 'inherit',
    stderr: 'inherit',
    env: {
      ...process.env,
      E2E_VITE_PORT: vitePort,
      E2E_CATALOG_URL: process.env.E2E_CATALOG_URL ?? 'http://127.0.0.1:5111',
      E2E_BOOKING_URL: process.env.E2E_BOOKING_URL ?? 'http://localhost:5102',
    },
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
