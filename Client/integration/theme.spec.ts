import { expect, test } from "./support/test";

const html = (page: import("@playwright/test").Page) => page.locator("html");

test.describe("themes", () => {
  test("start dark and remember light mode across visits", async ({ page }) => {
    await page.goto("/sign-in");
    const toggle = page.getByRole("button", { name: "Dark mode" });
    await expect(toggle).toHaveAttribute("aria-pressed", "true");
    await expect(html(page)).toHaveAttribute("data-theme", "dark");
    await expect(page.locator("body")).toHaveCSS("background-color", "rgb(20, 22, 27)");

    await toggle.click();

    await expect(toggle).toHaveAttribute("aria-pressed", "false");
    await expect(page.locator("body")).toHaveCSS("background-color", "rgb(245, 246, 248)");

    await page.reload();
    await expect(html(page)).toHaveAttribute("data-theme", "light");
    await expect(page.getByRole("button", { name: "Dark mode" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
    // Exactly one tag, even though the boot script set it before React hydrated.
    await expect(page.locator('meta[name="theme-color"]')).toHaveCount(1);
    await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute("content", "#ffffff");
  });

  test("apply a saved light theme before the app's code runs", async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem("jal.theme", "light");
    });
    // Block the app's scripts and styles: only the prerendered HTML and its
    // inline boot script are left.
    await page.route("**/assets/**", (route) => route.abort());

    await page.goto("/sign-in");

    await expect(page.getByText("Loading JustAnotherList…")).toBeAttached();
    await expect(html(page)).toHaveAttribute("data-theme", "light");
    await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute("content", "#ffffff");
  });
});
