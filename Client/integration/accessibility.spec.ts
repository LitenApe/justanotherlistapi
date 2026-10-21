import {
  expect,
  expectAccessible,
  makeItem,
  makeList,
  ME,
  OTHER,
  test,
  type App,
} from "./support/test";

/** A list that shows most of the list page's states at once. */
function busyList() {
  const list = makeList("Groceries", ["Milk", "Eggs", "Bread"], [ME, OTHER]);
  list.items.push(makeItem({ name: "Coffee", itemGroupId: list.id, isComplete: true }));
  const [milk] = list.items;
  if (milk) milk.description = "Oat, 2 litres";
  list.settings.autoDeleteAfterMinutes = 7 * 24 * 60;
  return list;
}

async function useTheme(app: App, theme: "dark" | "light") {
  await app.page.addInitScript((value) => {
    localStorage.setItem("jal.theme", value);
  }, theme);
}

for (const theme of ["dark", "light"] as const) {
  test.describe(`${theme} theme`, () => {
    test("pages pass axe, colour contrast included", async ({ page, app, backend }) => {
      await useTheme(app, theme);
      await page.goto("/sign-in");
      await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
      await expectAccessible(page);

      const list = busyList();
      backend.seed(list, makeList("Garden"));
      await app.signIn();
      await expectAccessible(page);

      await page.goto(`/lists/${list.id}`);
      await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeVisible();
      await expectAccessible(page);
    });

    test("dialogs and messages pass axe", async ({ page, app, backend }) => {
      await useTheme(app, theme);
      const list = busyList();
      await app.openList(list);

      await page.getByRole("button", { name: "Edit “Milk”" }).click();
      await page.getByLabel("Name").fill("");
      await page.getByRole("button", { name: "Save" }).click();
      await expect(page.getByLabel("Name")).toHaveAttribute("aria-invalid", "true");
      await expectAccessible(page, "dialog[open]");
      await page.keyboard.press("Escape");

      await page.getByRole("button", { name: "Settings" }).click();
      await expectAccessible(page, "dialog[open]");
      await page.keyboard.press("Escape");

      await page.getByRole("button", { name: "Delete list" }).click();
      await expectAccessible(page, "dialog[open]");
      await page.keyboard.press("Escape");

      backend.failNext("PUT", /\/api\/list\/.+\/.+/, 500);
      // click, not check(): the server rejects it, so the box flips back.
      await page.getByRole("checkbox", { name: "Eggs" }).click();
      await expect(page.getByRole("alert").filter({ hasText: "Couldn't update" })).toBeVisible();
      await page.getByRole("button", { name: "Account" }).click();
      await expectAccessible(page);
    });
  });
}

test.describe("keyboard", () => {
  test("the skip link is first and moves focus to the main content", async ({ page, app }) => {
    await app.signIn();
    await page.reload();
    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();

    await page.keyboard.press("Tab");
    const skip = page.getByRole("link", { name: "Skip to main content" });
    await expect(skip).toBeFocused();
    await expect(skip).toBeInViewport();
    await page.keyboard.press("Enter");

    await expect(page.getByRole("main")).toBeFocused();
  });

  test("the account menu opens and closes from the keyboard", async ({ page, app }) => {
    await app.signIn();
    const account = page.getByRole("button", { name: "Account" });

    await account.focus();
    await page.keyboard.press("Enter");
    await expect(account).toHaveAttribute("aria-expanded", "true");
    await expect(page.getByRole("button", { name: "Sign out" })).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(account).toHaveAttribute("aria-expanded", "false");
    await expect(account).toBeFocused();
  });
});

test.describe("small screens", () => {
  test("content reflows at 320px without scrolling sideways", async ({ page, app }) => {
    await page.setViewportSize({ width: 320, height: 700 });
    await app.openList(busyList());

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBe(0);
    // The header stays on one row, with icon-only buttons that keep their names.
    const header = await page.getByRole("banner").boundingBox();
    expect(header?.height).toBeLessThan(70);
    await expect(page.getByRole("button", { name: "Dark mode" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Account" })).toBeVisible();
  });

  test("@mobile the members panel is one tap away on a phone", async ({ page, app }) => {
    await app.openList(busyList());

    const jump = page.getByRole("link", { name: "Members (2)" });
    await jump.click();

    await expect(page.getByRole("heading", { name: /Members/ })).toBeFocused();
    await expect(page.getByRole("heading", { name: /Members/ })).toBeInViewport();
  });

  test("@mobile controls are big enough to tap", async ({ page, app }) => {
    await app.openList(busyList());

    const tooSmall = await page.evaluate(() =>
      [...document.querySelectorAll<HTMLElement>("main button, main a, main input, main select")]
        .filter((element) => element.getClientRects().length > 0)
        .filter((element) => !element.closest(".visually-hidden"))
        .map((element) => ({ element, box: element.getBoundingClientRect() }))
        .filter(({ box }) => box.width < 24 || box.height < 24)
        .map(({ element }) => element.outerHTML.slice(0, 80)),
    );
    expect(tooSmall).toEqual([]);
  });
});
