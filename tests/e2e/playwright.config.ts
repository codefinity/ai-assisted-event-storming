import { defineConfig, devices } from '@playwright/test';

/**
 * Runs against a stack that is already up: `docker compose up` (or the API and `npm run dev`).
 * E2E_BASE_URL and E2E_API_URL point it elsewhere.
 */
export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.ts',
  timeout: 90_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  globalSetup: './global-setup.ts',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
    trace: 'retain-on-failure',
    viewport: { width: 1280, height: 800 },
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } } }],
});
