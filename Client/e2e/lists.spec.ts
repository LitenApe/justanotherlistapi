import { expect, test } from "./support/test";

test.describe("lists and items", () => {
  test("a list is created, renamed and deleted on the server", async ({ newUser }) => {
    const user = await newUser();
    const { page } = user;

    const listId = await user.createList("Groceries");
    await page.reload();
    await expect(page.getByRole("heading", { level: 1, name: "Groceries" })).toBeVisible();

    await page.getByRole("button", { name: "Rename" }).click();
    const name = page.getByRole("dialog", { name: "Rename list" }).getByLabel("List name");
    await name.fill("Weekly shop");
    await name.press("Enter");
    await expect(page.getByRole("heading", { level: 1, name: "Weekly shop" })).toBeVisible();
    await page.goto("/");
    await expect(page.getByRole("link", { name: "Weekly shop" })).toBeVisible();

    await page.getByRole("link", { name: "Weekly shop" }).click();
    await page.getByRole("button", { name: "Delete list" }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Delete list" }).click();
    await expect(page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();

    await page.reload();
    await expect(page.getByRole("heading", { name: "No lists yet" })).toBeVisible();
    // The API now refuses the list: it no longer exists, so nobody is a member.
    await page.goto(`/lists/${listId}`);
    await expect(
      page.getByRole("heading", { level: 1, name: "You can't open this list" }),
    ).toBeVisible();
  });

  test("items keep their names, notes and done state", async ({ newUser }) => {
    const user = await newUser();
    const { page } = user;
    await user.createList("Groceries");

    await user.addItem("Milk");
    await user.addItem("Eggs");
    await user.addItem("Bread");
    await user.toggle("Eggs");

    await page.getByRole("button", { name: "Edit “Milk”" }).click();
    const editor = page.getByRole("dialog", { name: "Edit item" });
    await editor.getByLabel(/Notes/).fill("Oat, 2 litres");
    await editor.getByRole("button", { name: "Save" }).click();
    await expect(editor).toBeHidden();

    await page.getByRole("button", { name: "Edit “Bread”" }).click();
    await page.getByRole("button", { name: "Delete item" }).click();
    await page
      .getByRole("dialog", { name: "Delete “Bread”?" })
      .getByRole("button", { name: "Delete item" })
      .click();
    await expect(page.getByRole("checkbox", { name: "Bread" })).toHaveCount(0);

    await page.reload();
    await expect(
      page.getByRole("checkbox", { name: "Milk", exact: true }),
    ).toHaveAccessibleDescription("Oat, 2 litres");
    await expect(page.getByRole("checkbox", { name: "Eggs", exact: true })).toBeChecked();
    await expect(page.getByRole("checkbox", { name: "Bread" })).toHaveCount(0);
    await expect(page.getByText("1 of 2 done")).toBeVisible();

    // The overview only counts what's left to do.
    await page.goto("/");
    await expect(page.getByRole("link", { name: "Groceries" })).toHaveAccessibleDescription(
      /1 item to do.*Milk/,
    );
  });

  test("a reopened item counts as to do again", async ({ newUser }) => {
    const user = await newUser();
    const { page } = user;
    await user.createList("Chores");
    await user.addItem("Vacuum");

    await user.toggle("Vacuum");
    await user.toggle("Vacuum");
    await page.reload();

    await expect(page.getByRole("checkbox", { name: "Vacuum" })).not.toBeChecked();
    await expect(page.getByText("0 of 1 done")).toBeVisible();
  });
});
