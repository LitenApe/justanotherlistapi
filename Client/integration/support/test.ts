import AxeBuilder from "@axe-core/playwright";
import { test as base, expect, type Page } from "@playwright/test";
import type { ItemGroup } from "../../src/api/types";
import { ME } from "../../src/test/builders";
import { FakeBackend } from "./fakeBackend";

export { expect };
export * from "../../src/test/builders";

/** Drives the app the way a person would. */
export class App {
  constructor(
    readonly page: Page,
    readonly backend: FakeBackend,
  ) {}

  /** Signs in through the real sign-in page and lands on "Your lists". */
  async signIn(userId = ME): Promise<void> {
    await this.page.goto("/sign-in");
    await this.page.getByLabel("User ID").fill(userId);
    await this.page.getByRole("button", { name: "Sign in" }).click();
    await expect(this.page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
  }

  /** Seeds the list, signs in and opens it, ready for live updates. */
  async openList(list: ItemGroup, userId = ME): Promise<void> {
    if (!this.backend.list(list.id)) this.backend.seed(list);
    await this.signIn(userId);
    await this.page.goto(`/lists/${list.id}`);
    await expect(this.page.getByRole("heading", { level: 2, name: "Items" })).toBeVisible();
    await this.subscribed(list.id, userId);
  }

  /**
   * Waits until the page listens to the list's live updates. A change another
   * member makes before that is (correctly) missed, so tests wait first.
   */
  async subscribed(listId: string, userId = ME): Promise<void> {
    await expect.poll(() => this.backend.isSubscribed(listId, userId)).toBe(true);
  }
}

export const test = base.extend<{ backend: FakeBackend; app: App }>({
  // Any uncaught error in the page fails the test, even if the UI looked fine.
  page: async ({ page }, use) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await use(page);
    expect(errors, "uncaught errors in the page").toEqual([]);
  },
  // Always installed, so no test can reach a real (or missing) server.
  backend: [
    async ({ page }, use) => {
      const backend = new FakeBackend();
      await backend.install(page);
      await use(backend);
    },
    { auto: true },
  ],
  app: async ({ page, backend }, use) => {
    await use(new App(page, backend));
  },
});

/**
 * Runs every axe-core rule (WCAG 2.2 A/AA, including colour contrast against
 * the real styles, plus best practices) on the page or a part of it.
 */
export async function expectAccessible(page: Page, selector?: string): Promise<void> {
  // Colours are partly transparent while dialogs and toasts fade in.
  await page.evaluate(() => Promise.all(document.getAnimations().map((a) => a.finished)));
  const builder = new AxeBuilder({ page });
  if (selector) builder.include(selector);
  const { violations } = await builder.analyze();
  expect(
    violations.map(
      (violation) =>
        `${violation.id}: ${violation.help} (${violation.nodes
          .map((node) => node.target.join(" "))
          .join(", ")})`,
    ),
  ).toEqual([]);
}
