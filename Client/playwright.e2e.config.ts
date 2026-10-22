import { defineConfig, devices } from "@playwright/test";
import { CLIENT_PORT } from "./e2e/support/stack";

/**
 * End-to-end tests: the real client, the real Core backend, SQL Server and the
 * mock OAuth server, all started locally by e2e/support/globalSetup.ts.
 * Needs Docker and the .NET SDK.
 */
export default defineConfig({
  testDir: "e2e",
  globalSetup: "./e2e/support/globalSetup.ts",
  outputDir: "test-results/e2e",
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  workers: process.env.CI ? 2 : 4,
  // Real network, database and SignalR: allow more time than the faked tests.
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [["github"], ["list"]] : "list",
  use: {
    baseURL: `http://localhost:${CLIENT_PORT.toString()}`,
    trace: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
