import { expect, expectAccessible, makeItem, makeList, ME, OTHER, test } from "./support/test";

test.describe("your lists", () => {
  test("shows your lists by name with what's left to do", async ({ page, backend, app }) => {
    backend.seed(
      makeList("groceries", ["Milk", "Eggs", "Bread", "Butter"]),
      makeList("Garden"),
      makeList("Someone else's", ["Secret"], [OTHER]),
    );

    await app.signIn();

    const cards = page.getByRole("main").getByRole("link");
    await expect(cards).toHaveText(["Garden", "groceries"]);
    await expect(page.getByRole("link", { name: "groceries" })).toHaveAccessibleDescription(
      /4 items to do.*Milk.*and 1 more/,
    );
    await expect(page.getByRole("link", { name: "Garden" })).toHaveAccessibleDescription(
      "All done",
    );
    await expect(page).toHaveTitle("Your lists – JustAnotherList");
    await expectAccessible(page);
  });

  test("shows a friendly empty state", async ({ page, app }) => {
    await app.signIn();

    await expect(page.getByRole("heading", { name: "No lists yet" })).toBeVisible();
    await expectAccessible(page);
  });

  test("creates a list and opens it", async ({ page, backend, app }) => {
    await app.signIn();

    await page.getByLabel("New list").fill("Holiday packing");
    await page.getByRole("button", { name: "Create list" }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Holiday packing" })).toBeFocused();
    const created = backend.lists.find((list) => list.name === "Holiday packing");
    expect(created?.members).toEqual([ME]);
    await expect(page).toHaveURL(`/lists/${created?.id ?? ""}`);
  });

  test("requires a name before creating a list", async ({ page, backend, app }) => {
    await app.signIn();
    const input = page.getByLabel("New list");

    await page.getByRole("button", { name: "Create list" }).click();

    await expect(input).toHaveAttribute("aria-invalid", "true");
    await expect(input).toHaveAccessibleDescription("Give your list a name.");
    await expect(input).toBeFocused();
    await expect(page).toHaveURL("/");
    expect(backend.callsTo("POST", /^\/api\/list$/)).toEqual([]);
    await expectAccessible(page);
  });

  test("offers a retry when loading fails", async ({ page, backend, app }) => {
    backend.seed(makeList("Groceries"));
    await app.signIn();
    backend.failNext("GET", /^\/api\/list$/, 500);

    await page.reload();

    const alert = page.getByRole("alert").filter({ hasText: "Couldn't load your lists" });
    await expect(alert).toContainText("The server ran into a problem");
    await expectAccessible(page);
    await alert.getByRole("button", { name: "Try again" }).click();

    await expect(page.getByRole("link", { name: "Groceries" })).toBeVisible();
  });

  test("keeps cards current with changes other members make", async ({ page, backend, app }) => {
    const groceries = makeList("Groceries", ["Milk"], [ME, OTHER]);
    const chores = makeList("Chores", ["Vacuum"], [ME, OTHER]);
    backend.seed(groceries, chores);
    await app.signIn();
    await app.subscribed(groceries.id);
    await app.subscribed(chores.id);

    const other = backend.as(OTHER);
    other.createItem(groceries.id, { name: "Eggs" });
    other.renameList(groceries.id, "Weekly shop");
    other.deleteList(chores.id);

    await expect(page.getByRole("link", { name: "Weekly shop" })).toHaveAccessibleDescription(
      /2 items to do/,
    );
    await expect(page.getByRole("link", { name: "Chores" })).toHaveCount(0);
  });

  test("picks up lists shared with you when you return to the tab", async ({
    page,
    backend,
    app,
  }) => {
    const theirs = makeList("Their list", [], [OTHER]);
    backend.seed(theirs);
    await app.signIn();
    await expect(page.getByRole("heading", { name: "No lists yet" })).toBeVisible();

    backend.as(OTHER).addMember(theirs.id, ME);
    // What the browser fires when you switch back to this tab.
    await page.evaluate(() => document.dispatchEvent(new Event("visibilitychange")));

    await expect(page.getByRole("link", { name: "Their list" })).toBeVisible();
  });

  test("filters long collections of lists", async ({ page, backend, app }) => {
    backend.seed(...Array.from({ length: 8 }, (_, i) => makeList(`List ${i + 1}`)));
    await app.signIn();

    await page.getByRole("searchbox", { name: "Find a list" }).fill("7");

    await expect(page.getByRole("main").getByRole("link")).toHaveText(["List 7"]);
    await expect(page.getByText("Showing 1 of 8 lists")).toBeVisible();
  });

  test("opens a list from its card", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"]);
    list.items.push(makeItem({ name: "Eggs", itemGroupId: list.id, isComplete: true }));
    backend.seed(list);
    await app.signIn();

    await page.getByRole("link", { name: "Groceries" }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeFocused();
    await expect(page.getByText("1 of 2 done")).toBeVisible();
    await page.goBack();
    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeFocused();
  });
});
