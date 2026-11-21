import { expect, expectAccessible, makeList, ME, OTHER, test } from "./support/test";

test.describe("changing your name", () => {
  test("shows the new name in the account menu and on the page straight away", async ({
    page,
    backend,
    app,
  }) => {
    await app.openList(makeList("Groceries", [], [ME, OTHER]));
    const account = page.getByRole("button", { name: "Account" });

    await account.click();
    await page.getByRole("button", { name: "Change name" }).click();
    const dialog = page.getByRole("dialog", { name: "Change your name" });
    await expect(dialog.getByLabel("Name")).toHaveValue("Robin");
    await expect(dialog.getByLabel("Name")).toBeFocused();
    await expectAccessible(page, "dialog[open]");
    await dialog.getByLabel("Name").fill("Robin Lee");
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(dialog).toBeHidden();
    await expect(account).toBeFocused();
    await expect(page.getByRole("status").getByText("Your name is now Robin Lee.")).toBeAttached();
    const members = page.getByRole("region", { name: /Members/ });
    await expect(members.getByText("Robin Lee", { exact: true })).toBeVisible();
    expect(backend.accountById(ME).name).toBe("Robin Lee");
    await account.click();
    await expect(page.getByText("Robin Lee", { exact: true }).first()).toBeVisible();
  });

  test("explains a name that breaks the rule, and changes nothing", async ({
    page,
    backend,
    app,
  }) => {
    await app.signIn();
    await page.getByRole("button", { name: "Account" }).click();
    await page.getByRole("button", { name: "Change name" }).click();
    const dialog = page.getByRole("dialog", { name: "Change your name" });

    await dialog.getByLabel("Name").fill("  ");
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(dialog.getByLabel("Name")).toHaveAccessibleDescription("Enter your name.");
    await expectAccessible(page, "dialog[open]");
    expect(backend.accountById(ME).name).toBe("Robin");
    expect(backend.calls.filter((call) => call.path === "/api/me")).toEqual([]);
  });

  test("keeps the old name for the other members until they load the list again", async ({
    page,
    backend,
    app,
  }) => {
    const list = makeList("Groceries", [], [ME, OTHER]);
    await app.openList(list);

    // OTHER (Kim) renames themselves elsewhere: no event comes, as with the real API.
    backend.accountById(OTHER).name = "Kim Lee";
    backend.list(list.id)?.members.forEach((member) => {
      if (member.id === OTHER) member.name = "Kim Lee";
    });
    const members = page.getByRole("region", { name: /Members/ });
    await expect(members.getByText("Kim", { exact: true })).toBeVisible();

    await page.reload();
    await expect(members.getByText("Kim Lee", { exact: true })).toBeVisible();
  });
});
