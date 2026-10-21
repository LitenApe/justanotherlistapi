import { defineConfig, devices } from "@playwright/test";

const port = 4173;

/**
 * Integration tests: the production SPA build served as static files, driven
 * in a real browser. Only the network (REST, OAuth token endpoint, SignalR
 * WebSocket) is faked, in integration/support/fakeBackend.ts.
 */
export default defineConfig({
  testDir: "integration",
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  reporter: process.env.CI ? [["github"], ["list"]] : "list",
  use: {
    baseURL: `http://localhost:${port}`,
    trace: "retain-on-failure",
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] }, grepInvert: /@mobile/ },
    // Phone-specific checks; tag a test @mobile to run it here instead.
    { name: "mobile", use: { ...devices["Pixel 7"] }, grep: /@mobile/ },
  ],
  webServer: {
    command: `npx react-router build && node integration/support/serveSpa.mjs ${port}`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
