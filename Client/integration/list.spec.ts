import { expect, expectAccessible, makeList, ME, OTHER, test } from "./support/test";

const itemPath = /^\/api\/list\/[^/]+\/[0-9a-f-]{36}$/;

test.describe("a list", () => {
  test("shows items, progress and members", async ({ page, app }) => {
    const list = makeList("Groceries", ["Milk", "Eggs"], [ME, OTHER]);
    const [milk] = list.items;
    if (milk) milk.description = "Oat milk";

    await app.openList(list);

    await expect(page).toHaveTitle("Groceries – JustAnotherList");
    await expect(page.getByText("0 of 2 done")).toBeVisible();
    await expect(page.getByRole("checkbox", { name: "Milk" })).toHaveAccessibleDescription(
      "Oat milk",
    );
    await expect(page.getByRole("button", { name: "Edit “Milk”" })).toBeVisible();
    const members = page.getByRole("region", { name: /Members/ });
    await expect(members.getByText("You", { exact: true })).toBeVisible();
    await expect(members.getByRole("button", { name: `Remove member ${OTHER}` })).toBeVisible();
    await expectAccessible(page);
  });

  test("keeps the current page, marked busy, while the next one loads", async ({
    page,
    backend,
    app,
  }) => {
    const list = makeList("Groceries", ["Milk"]);
    backend.seed(list);
    await app.signIn();
    const release = backend.holdNext("GET", new RegExp(`/api/list/${list.id}$`));

    await page.getByRole("link", { name: "Groceries" }).click();

    await expect(page.getByRole("main")).toHaveAttribute("aria-busy", "true");
    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    release();
    await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeFocused();
    await expect(page.getByRole("main")).not.toHaveAttribute("aria-busy");
  });

  test("announces loading when opened directly", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"]);
    backend.seed(list);
    await app.signIn();
    const release = backend.holdNext("GET", new RegExp(`/api/list/${list.id}$`));

    await page.goto(`/lists/${list.id}`);

    await expect(
      page.getByRole("status").filter({ hasText: "Loading JustAnotherList…" }),
    ).toBeVisible();
    release();
    await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeVisible();
  });

  test("toggles an item and saves the whole item", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"]);
    await app.openList(list);
    const checkbox = page.getByRole("checkbox", { name: "Milk" });

    await checkbox.check();

    await expect(checkbox).toBeChecked();
    await expect(page.getByText("1 of 1 done")).toBeVisible();
    await expect
      .poll(() => backend.callsTo("PUT", itemPath).map((call) => call.body))
      .toEqual([{ name: "Milk", description: null, isComplete: true }]);
    expect(backend.list(list.id)?.items[0]?.completedAt).not.toBeNull();
  });

  test("reverts a toggle the server rejects", async ({ page, backend, app }) => {
    await app.openList(makeList("Groceries", ["Milk"]));
    backend.failNext("PUT", itemPath, 500);

    await page.getByRole("checkbox", { name: "Milk" }).click();

    await expect(
      page.getByRole("alert").filter({ hasText: "Couldn't update “Milk”" }),
    ).toBeVisible();
    await expect(page.getByRole("checkbox", { name: "Milk" })).not.toBeChecked();
  });

  test("moves focus on when a toggled item leaves the filtered view", async ({ page, app }) => {
    await app.openList(makeList("Groceries", ["Milk", "Eggs", "Bread"]));

    // The radio is visually hidden inside its segment; people click the segment.
    await page.locator("label", { has: page.getByRole("radio", { name: /To do/ }) }).click();
    // click, not check(): the item leaves the "To do" view as soon as it's checked.
    await page.getByRole("checkbox", { name: "Eggs" }).click();

    await expect(page.getByRole("checkbox", { name: "Eggs" })).toHaveCount(0);
    await expect(page.getByRole("checkbox", { name: "Bread" })).toBeFocused();
    await expect(page.getByText("Showing 2 of 3 items")).toBeVisible();
  });

  test("searches item names and notes", async ({ page, app }) => {
    const list = makeList("Groceries", ["Milk", "Eggs"]);
    const [, eggs] = list.items;
    if (eggs) eggs.description = "Free range";
    await app.openList(list);

    await page.getByRole("searchbox", { name: "Search" }).fill("range");

    await expect(page.getByRole("list", { name: "Items" }).getByRole("checkbox")).toHaveCount(1);
    await expect(page.getByRole("checkbox", { name: "Eggs" })).toBeVisible();
    await expect(page.getByText("Showing 1 of 2 items")).toBeVisible();
  });

  test("adds items to the top and keeps focus in the field", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"]);
    await app.openList(list);
    const input = page.getByLabel("Add an item");

    await input.fill("Coffee");
    await input.press("Enter");
    await input.fill("Tea");
    await input.press("Enter");

    const items = page.getByRole("list", { name: "Items" }).getByRole("checkbox");
    await expect(items).toHaveCount(3);
    await expect(items.first()).toHaveAccessibleName("Tea");
    await expect(input).toHaveValue("");
    await expect(input).toBeFocused();
    await expect
      .poll(() => backend.list(list.id)?.items.map((item) => item.name))
      .toEqual(["Milk", "Coffee", "Tea"]);
  });

  test("edits an item in a dialog", async ({ page, backend, app }) => {
    await app.openList(makeList("Groceries", ["Milk"]));
    const edit = page.getByRole("button", { name: "Edit “Milk”" });

    await edit.click();
    const dialog = page.getByRole("dialog", { name: "Edit item" });
    await expect(dialog.getByLabel("Name")).toBeFocused();
    await expectAccessible(page, "dialog");
    await dialog.getByLabel("Name").fill("Oat milk");
    await dialog.getByLabel(/Notes/).fill("Barista edition");
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(dialog).toBeHidden();
    await expect(page.getByRole("checkbox", { name: "Oat milk" })).toHaveAccessibleDescription(
      "Barista edition",
    );
    await expect(page.getByRole("button", { name: "Edit “Oat milk”" })).toBeFocused();
    expect(backend.callsTo("PUT", itemPath).map((call) => call.body)).toEqual([
      { name: "Oat milk", description: "Barista edition", isComplete: false },
    ]);
  });

  test("confirms before deleting an item, then focuses its neighbour", async ({
    page,
    backend,
    app,
  }) => {
    const list = makeList("Groceries", ["Milk", "Eggs"]);
    await app.openList(list);

    await page.getByRole("button", { name: "Edit “Milk”" }).click();
    await page.getByRole("button", { name: "Delete item" }).click();
    const confirm = page.getByRole("dialog", { name: "Delete “Milk”?" });
    await expect(confirm.getByRole("button", { name: "Cancel" })).toBeFocused();
    await expectAccessible(page, "dialog[open]");
    await confirm.getByRole("button", { name: "Delete item" }).click();

    await expect(page.getByRole("checkbox", { name: "Milk" })).toHaveCount(0);
    await expect(page.getByRole("checkbox", { name: "Eggs" })).toBeFocused();
    expect(backend.list(list.id)?.items.map((item) => item.name)).toEqual(["Eggs"]);
  });

  test("renames the list", async ({ page, backend, app }) => {
    const list = makeList("Groceries");
    await app.openList(list);

    await page.getByRole("button", { name: "Rename" }).click();
    const input = page.getByRole("dialog", { name: "Rename list" }).getByLabel("List name");
    await input.fill("Weekly shop");
    await input.press("Enter");

    await expect(page.getByRole("heading", { level: 1, name: "Weekly shop" })).toBeVisible();
    await expect(page).toHaveTitle("Weekly shop – JustAnotherList");
    expect(backend.list(list.id)?.name).toBe("Weekly shop");
  });

  test("deletes the list after confirmation and returns to the overview", async ({
    page,
    backend,
    app,
  }) => {
    const list = makeList("Groceries", ["Milk"]);
    await app.openList(list);

    await page.getByRole("button", { name: "Delete list" }).click();
    const confirm = page.getByRole("dialog", { name: "Delete “Groceries”?" });
    await expect(confirm).toHaveAccessibleDescription(/its 1 item for every member/);
    await confirm.getByRole("button", { name: "Delete list" }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeFocused();
    await expect(page.getByRole("status").getByText("Deleted “Groceries”.")).toBeVisible();
    expect(backend.list(list.id)).toBeUndefined();
  });

  test("adds members by user ID and explains conflicts", async ({ page, backend, app }) => {
    const list = makeList("Groceries", [], [ME, OTHER]);
    await app.openList(list);
    const input = page.getByLabel("Add a member");

    await input.fill(OTHER);
    await page.getByRole("button", { name: "Add member" }).click();
    await expect(input).toHaveAccessibleDescription(/That person is already a member/);

    const newMember = "3F2504E0-4F89-41D3-9A0C-0305E82C3301";
    await input.fill(newMember);
    await page.getByRole("button", { name: "Add member" }).click();

    await expect(page.getByText(newMember.toLowerCase(), { exact: true })).toBeVisible();
    await expect(input).toHaveValue("");
    expect(backend.list(list.id)?.members).toEqual([ME, OTHER, newMember.toLowerCase()]);
  });

  test("leaves a shared list after confirmation", async ({ page, backend, app }) => {
    const list = makeList("Groceries", [], [ME, OTHER]);
    await app.openList(list);

    await page.getByRole("button", { name: "Leave list" }).click();
    await page
      .getByRole("dialog", { name: "Leave “Groceries”?" })
      .getByRole("button", { name: "Leave list" })
      .click();

    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    await expect(page.getByRole("status").getByText("You left “Groceries”.")).toBeVisible();
    expect(backend.list(list.id)?.members).toEqual([OTHER]);
  });

  test("only offers to leave when someone else remains a member", async ({ page, app }) => {
    await app.openList(makeList("Solo"));
    await expect(page.getByRole("button", { name: "Leave list" })).toHaveCount(0);
  });

  test("offers a retry when the list fails to load", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"]);
    backend.seed(list);
    await app.signIn();
    backend.failNext("GET", new RegExp(`/api/list/${list.id}$`), 500);

    await page.goto(`/lists/${list.id}`);

    await expect(
      page.getByRole("heading", { level: 1, name: "Couldn't load this list" }),
    ).toBeVisible();
    await expectAccessible(page);
    await page.getByRole("button", { name: "Try again" }).click();
    await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeVisible();
  });

  test("explains when you don't have access", async ({ page, backend, app }) => {
    const secret = makeList("Secret", [], [OTHER]);
    backend.seed(secret);
    await app.signIn();

    await page.goto(`/lists/${secret.id}`);

    await expect(
      page.getByRole("heading", { level: 1, name: "You can't open this list" }),
    ).toBeVisible();
    await expect(page.getByText("Secret")).toHaveCount(0);
    await expectAccessible(page);
  });

  test("handles addresses that aren't lists", async ({ page, backend, app }) => {
    await app.signIn();
    const callsBefore = backend.calls.length;

    await page.goto("/lists/not-a-guid");

    await expect(page.getByRole("heading", { level: 1, name: "List not found" })).toBeVisible();
    expect(backend.calls).toHaveLength(callsBefore);
    await page.goto("/no/such/page");
    await expect(page.getByRole("heading", { level: 1, name: "Page not found" })).toBeVisible();
    await expectAccessible(page);
  });
});
