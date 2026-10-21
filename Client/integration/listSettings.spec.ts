import {
  ago,
  days,
  expect,
  expectAccessible,
  makeItem,
  makeList,
  ME,
  minutes,
  OTHER,
  test,
} from "./support/test";

test.describe("list settings", () => {
  test("saves settings for everyone and summarises them", async ({ page, backend, app }) => {
    const list = makeList("Groceries", ["Milk"]);
    await app.openList(list);

    await page.getByRole("button", { name: "Settings" }).click();
    const dialog = page.getByRole("dialog", { name: "List settings" });
    await expectAccessible(page, "dialog[open]");
    await dialog.getByRole("checkbox", { name: "Hide done items" }).check();
    const hideAfter = dialog.getByRole("group", { name: "Hide after" });
    await hideAfter.getByLabel("Amount").fill("2");
    await hideAfter.getByLabel("Unit").selectOption("hours");
    await dialog.getByRole("checkbox", { name: "Delete the list when every item is done" }).check();
    await dialog.getByRole("group", { name: "Delete after" }).getByLabel("Amount").fill("12");
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(dialog).toBeHidden();
    await expect(
      page.getByText(
        "Done items are hidden after 2 hours. The list is deleted 12 days after every item is done.",
      ),
    ).toBeVisible();
    expect(backend.list(list.id)?.settings).toMatchObject({
      hideCompletedAfterMinutes: 120,
      autoDeleteAfterMinutes: 12 * 24 * 60,
    });
  });

  test("hides done items after the chosen time but keeps them under Done", async ({
    page,
    app,
  }) => {
    const list = makeList("Groceries");
    list.settings.hideCompletedAfterMinutes = 60;
    list.items = [
      makeItem({ name: "Milk", itemGroupId: list.id, isComplete: true, completedAt: ago(days(1)) }),
      makeItem({ name: "Eggs", itemGroupId: list.id, isComplete: true }),
      makeItem({ name: "Bread", itemGroupId: list.id }),
    ];
    await app.openList(list);

    await expect(page.getByRole("checkbox", { name: "Milk" })).toHaveCount(0);
    await expect(page.getByRole("checkbox", { name: "Eggs" })).toBeChecked();
    await expect(page.getByText("2 of 3 done")).toBeVisible();
    await expect(page.getByText("Showing 2 of 3 items")).toBeVisible();
    await expect(page.getByText("Done items are hidden after 1 hour.")).toBeVisible();
    await expectAccessible(page);

    await page.getByRole("button", { name: "Show done items" }).click();

    const done = page.getByRole("radio", { name: /^Done/ });
    await expect(done).toBeChecked();
    await expect(done).toBeFocused();
    await expect(page.getByRole("checkbox", { name: "Milk" })).toBeChecked();
  });

  test("hides an item as soon as it's checked when set to hide right away", async ({
    page,
    backend,
    app,
  }) => {
    const list = makeList("Groceries", ["Milk", "Eggs"]);
    list.settings.hideCompletedAfterMinutes = 0;
    await app.openList(list);

    await page.getByRole("checkbox", { name: "Milk" }).click();

    await expect(page.getByRole("checkbox", { name: "Milk" })).toHaveCount(0);
    await expect(page.getByRole("checkbox", { name: "Eggs" })).toBeFocused();
    await expect(page.getByText(/1 done item is hidden/)).toBeVisible();
    await expect.poll(() => backend.list(list.id)?.items[0]?.isComplete).toBe(true);
  });

  test("hides a done item when its time comes and moves focus off it", async ({ page, app }) => {
    const list = makeList("Groceries");
    list.settings.hideCompletedAfterMinutes = 1;
    list.items = [
      // Due to hide a moment after the page opens; the real clock gets there.
      makeItem({
        name: "Milk",
        itemGroupId: list.id,
        isComplete: true,
        completedAt: ago(minutes(1) - 3_000),
      }),
      makeItem({ name: "Eggs", itemGroupId: list.id }),
    ];
    await app.openList(list);
    const milk = page.getByRole("checkbox", { name: "Milk" });
    await milk.focus();

    await expect(milk).toHaveCount(0, { timeout: 10_000 });
    await expect(page.getByRole("checkbox", { name: "Eggs" })).toBeFocused();
  });

  test("says when the list will be deleted once every item is done", async ({ page, app }) => {
    const list = makeList("Groceries");
    list.settings = {
      autoDeleteAfterMinutes: 24 * 60,
      hideCompletedAfterMinutes: null,
      autoDeleteUpdatedAt: ago(days(7)),
    };
    list.items = [
      makeItem({ name: "Milk", itemGroupId: list.id, isComplete: true }),
      makeItem({ name: "Eggs", itemGroupId: list.id }),
    ];
    await app.openList(list);
    await expect(
      page.getByText("The list is deleted 1 day after every item is done."),
    ).toBeVisible();
    const notice = page.getByRole("status").filter({ hasText: "will be deleted on" });
    await expect(notice).toHaveCount(0);

    await page.getByRole("checkbox", { name: "Eggs" }).check();

    await expect(notice).toContainText("Every item is done, so this list will be deleted on");
    await expectAccessible(page);
    await page.getByRole("checkbox", { name: "Eggs" }).uncheck();
    await expect(notice).toHaveCount(0);
  });

  test("applies settings other members change and explains automatic deletion", async ({
    page,
    backend,
    app,
  }) => {
    const list = makeList("Groceries", [], [ME, OTHER]);
    list.items = [
      makeItem({ name: "Milk", itemGroupId: list.id, isComplete: true, completedAt: ago(days(3)) }),
    ];
    await app.openList(list);

    backend.as(OTHER).updateSettings(list.id, {
      autoDeleteAfterMinutes: 24 * 60,
      hideCompletedAfterMinutes: null,
    });
    await expect(page.getByText("Another member changed the list settings.")).toBeVisible();
    await expect(page.getByText(/will be deleted on/)).toBeVisible();

    // The settings changed just now, so the countdown runs from now: in a day.
    const stored = backend.list(list.id);
    if (stored) stored.settings.autoDeleteUpdatedAt = ago(days(2));
    await page.reload();
    await app.subscribed(list.id);
    backend.deleteListAutomatically(list.id);

    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    await expect(
      page.getByText("“Groceries” was deleted automatically because every item was done."),
    ).toBeVisible();
  });
});
