import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  fullyParallel: false,
  workers: 1,
  retries: process.env['CI'] ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://localhost:4200',
    viewport: { width: 390, height: 844 },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command: 'dotnet run --launch-profile http --configuration Release --no-build',
      cwd: '../backend/Syssloappen.Api',
      url: 'http://localhost:5047/health',
      reuseExistingServer: !process.env['CI'],
      timeout: 120_000,
      // Force the logging email sender even when a developer's local user-secrets
      // set Email:Provider=Resend for manual testing — e2e reads confirmation and
      // reset links from /dev/last-email (see DevEmailStore), which only the
      // logging sender populates, and must never spend real Resend sends.
      env: { Email__Provider: 'Logging' },
    },
    {
      command:
        'node node_modules/@angular/cli/bin/ng.js serve --proxy-config proxy.conf.json --port 4200',
      cwd: '.',
      url: 'http://localhost:4200/login',
      reuseExistingServer: !process.env['CI'],
      timeout: 120_000,
    },
  ],
});
