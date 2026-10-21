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
 * Known issue: the app connects to SignalR with skipNegotiation, so it never
 * learns its connection ID and can't send X-SignalR-Connection-Id. The server
 * then echoes your own changes back as if another member made them. These
 * tests describe the intended behaviour and are expected to fail until that is
 * fixed; Playwright reports it as soon as one of them starts passing.
 */
test.describe("your own changes (known issue: echoed back)", () => {
  test("carry the realtime connection ID", async ({ page, backend, app }) => {
    test.fail(true, "The client never learns its SignalR connection ID.");
    const list = makeList("Groceries", ["Milk"]);
    await app.openList(list);

    await page.getByRole("checkbox", { name: "Milk" }).check();

    await expect.poll(() => backend.calls.at(-1)?.method).toBe("PUT");
    expect(backend.calls.at(-1)?.headers["x-signalr-connection-id"]).toBeTruthy();
  });

  test("aren't announced as another member's settings change", async ({ page, app }) => {
    test.fail(true, "The server echoes GroupSettingsChanged back to the sender.");
    await app.openList(makeList("Groceries"));

    await page.getByRole("button", { name: "Settings" }).click();
    await page.getByRole("checkbox", { name: "Hide done items" }).check();
    await page.getByRole("button", { name: "Save" }).click();

    await expect(page.getByText("Done items are hidden after 1 day.")).toBeVisible();
    await expect(page.getByText("Another member changed the list settings.")).toHaveCount(0);
  });

  test("don't report leaving a list as being removed", async ({ page, app }) => {
    test.fail(true, "The server echoes MemberRemoved back to the member who left.");
    await app.openList(makeList("Groceries", [], [ME, OTHER]));

    await page.getByRole("button", { name: "Leave list" }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Leave list" }).click();

    await expect(page.getByText("You left “Groceries”.")).toBeVisible();
    await expect(page.getByText("You were removed from “Groceries”.")).toHaveCount(0);
  });
});
