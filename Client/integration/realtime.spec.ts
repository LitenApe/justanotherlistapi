import { expect, makeList, ME, OTHER, test } from "./support/test";

test.describe("live updates", () => {
  test("show changes other members make as they happen", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk", "Eggs"], [ME, OTHER]);
    await app.openList(list);
    const [milk, eggs] = list.items;
    if (!milk || !eggs) throw new Error("missing items");

    const other = backend.as(OTHER);
    other.createItem(list.id, { name: "Bread" });
    other.updateItem(list.id, milk.id, { isComplete: true });
    other.deleteItem(list.id, eggs.id);
    other.renameList(list.id, "Weekly shop");
    other.addMember(list.id, "3f2504e0-4f89-41d3-9a0c-0305e82c3301");

    await expect(page.getByRole("heading", { level: 1, name: "Weekly shop" })).toBeVisible();
    await expect(page).toHaveTitle("Weekly shop – JustAnotherList");
    await expect(page.getByRole("checkbox", { name: "Bread" })).not.toBeChecked();
    await expect(page.getByRole("checkbox", { name: "Milk" })).toBeChecked();
    await expect(page.getByRole("checkbox", { name: "Eggs" })).toHaveCount(0);
    await expect(page.getByText("1 of 2 done")).toBeVisible();
    await expect(
      page
        .getByRole("region", { name: /Members/ })
        .getByText("3f2504e0-4f89-41d3-9a0c-0305e82c3301", {
          exact: true,
        }),
    ).toBeVisible();
  });

  test("move focus off an item another member deletes", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk", "Eggs"], [ME, OTHER]);
    await app.openList(list);
    const [milk] = list.items;
    await page.getByRole("checkbox", { name: "Milk" }).focus();

    backend.as(OTHER).deleteItem(list.id, milk?.id ?? "");

    await expect(page.getByRole("checkbox", { name: "Milk" })).toHaveCount(0);
    await expect(page.getByRole("checkbox", { name: "Eggs" })).toBeFocused();
  });

  test("close the editor when another member deletes that item", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"], [ME, OTHER]);
    await app.openList(list);
    await page.getByRole("button", { name: "Edit “Milk”" }).click();
    const dialog = page.getByRole("dialog", { name: "Edit item" });
    await expect(dialog).toBeVisible();

    backend.as(OTHER).deleteItem(list.id, list.items[0]?.id ?? "");

    await expect(dialog).toBeHidden();
    await expect(
      page.getByRole("alert").filter({ hasText: "The item you were editing was deleted" }),
    ).toBeVisible();
  });

  test("leave the page when you're removed from the list", async ({ page, backend, app }) => {
    const list = makeList("Groceries", [], [ME, OTHER]);
    await app.openList(list);

    backend.as(OTHER).removeMember(list.id, ME);

    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    await expect(page.getByText("You were removed from “Groceries”.")).toBeVisible();
    await expect(page.getByRole("link", { name: "Groceries" })).toHaveCount(0);
  });

  test("leave the page when another member deletes the list", async ({ page, backend, app }) => {
    const list = makeList("Groceries", [], [ME, OTHER]);
    await app.openList(list);

    backend.as(OTHER).deleteList(list.id);

    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    await expect(page.getByText("“Groceries” was deleted by another member.")).toBeVisible();
  });

  test("connect once per tab and disconnect when you sign out", async ({ page, backend, app }) => {
    const groceries = makeList("Groceries", [], [ME, OTHER]);
    const chores = makeList("Chores", [], [ME, OTHER]);
    backend.seed(groceries, chores);
    await app.signIn();
    await app.subscribed(groceries.id);
    await app.subscribed(chores.id);

    await page.getByRole("link", { name: "Groceries" }).click();
    await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeVisible();
    // The list page keeps listening to its list; the overview's other lists are let go.
    await expect.poll(() => backend.isSubscribed(chores.id)).toBe(false);
    expect(backend.isSubscribed(groceries.id)).toBe(true);
    expect(backend.hubConnectionCount()).toBe(1);

    await page.getByRole("button", { name: "Account" }).click();
    await page.getByRole("button", { name: "Sign out" }).click();
    await expect.poll(() => backend.hubConnectionCount()).toBe(0);
  });

  test("pause while disconnected, then catch up on what was missed", async ({
    page,
    backend,
    app,
  }) => {
    const list = makeList("Groceries", ["Milk"], [ME, OTHER]);
    await app.openList(list);
    const banner = page.getByText(/Live updates paused/);

    await backend.dropHubConnections();
    await expect(banner).toBeVisible();
    // Nobody is listening, so this change only reaches the page via the catch-up load.
    backend.as(OTHER).createItem(list.id, { name: "Bread" });

    await expect(banner).toBeHidden({ timeout: 10_000 });
    await expect(page.getByRole("checkbox", { name: "Bread" })).toBeVisible();
    expect(backend.isSubscribed(list.id)).toBe(true);
  });
});

/*
 * The app sends its SignalR connection ID (from negotiate) as
 * X-SignalR-Connection-Id, so the server leaves the sender out of the
 * broadcast and your own changes don't come back as another member's.
 */
test.describe("your own changes", () => {
  const itemUpdates = /^\/api\/list\/[^/]+\/[^/]+$/;

  test("carry the connection ID the server gave this tab", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"]);
    await app.openList(list);

    await page.getByRole("checkbox", { name: "Milk" }).check();

    await expect.poll(() => backend.callsTo("PUT", itemUpdates).length).toBe(1);
    const sent = backend.callsTo("PUT", itemUpdates)[0]?.headers["x-signalr-connection-id"];
    expect(backend.connectionIds(ME)).toEqual([sent]);
  });

  test("carry the new connection ID after reconnecting", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"]);
    await app.openList(list);
    const before = backend.connectionIds(ME);

    await backend.dropHubConnections();
    await expect(page.getByText(/Live updates paused/)).toBeHidden({ timeout: 10_000 });
    await app.subscribed(list.id);
    const after = backend.connectionIds(ME);
    expect(after).toHaveLength(1);
    expect(after).not.toEqual(before);

    await page.getByRole("checkbox", { name: "Milk" }).check();

    await expect.poll(() => backend.callsTo("PUT", itemUpdates).length).toBe(1);
    const sent = backend.callsTo("PUT", itemUpdates)[0]?.headers["x-signalr-connection-id"];
    expect([sent]).toEqual(after);
  });

  test("aren't announced as another member's settings change", async ({ page, app }) => {
    await app.openList(makeList("Groceries"));

    await page.getByRole("button", { name: "Settings" }).click();
    await page.getByRole("checkbox", { name: "Hide done items" }).check();
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByText("Done items are hidden after 1 day.")).toBeVisible();
    await expect(page.getByText("Another member changed the list settings.")).toHaveCount(0);
  });

  test("don't report leaving a list as being removed", async ({ page, app }) => {
    await app.openList(makeList("Groceries", [], [ME, OTHER]));

    await page.getByRole("button", { name: "Leave list" }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Leave list" }).click();

    await expect(page.getByText("You left “Groceries”.")).toBeVisible();
    await expect(page.getByText("You were removed from “Groceries”.")).toHaveCount(0);
  });

  test("don't report deleting a list as another member's doing", async ({ page, app }) => {
    await app.openList(makeList("Groceries", ["Milk"], [ME, OTHER]));

    await page.getByRole("button", { name: "Delete list" }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Delete list" }).click();

    await expect(page.getByText("Deleted “Groceries”.")).toBeVisible();
    await expect(page.getByText("“Groceries” was deleted by another member.")).toHaveCount(0);
  });
});
