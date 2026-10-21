import { playwright } from "@vitest/browser-playwright";
import { defineConfig } from "vitest/config";

/**
 * Unit and browser tests. Integration tests run with Playwright Test against
 * the production build instead (see playwright.config.ts).
 */
export default defineConfig({
  test: {
    projects: [
      {
        test: {
          // Pure logic: no DOM, no browser APIs, nothing mocked.
          name: "unit",
          include: ["src/**/*.unit.test.ts"],
          environment: "node",
        },
      },
      {
        test: {
          // Components and browser-API modules in a real Chromium, with real
          // events, focus, <dialog>, storage, clipboard and computed styles.
          name: "browser",
          include: ["src/**/*.browser.test.{ts,tsx}"],
          setupFiles: ["./src/test/browserSetup.ts"],
          browser: {
            enabled: true,
            headless: true,
            screenshotFailures: false,
            provider: playwright({
              contextOptions: { permissions: ["clipboard-read", "clipboard-write"] },
            }),
            instances: [{ browser: "chromium" }],
          },
        },
      },
    ],
  },
});
