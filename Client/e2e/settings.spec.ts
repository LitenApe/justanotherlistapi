import { expect, test } from "./support/test";

test.describe("list settings", () => {
  test("hiding done items applies to every member and survives a reload", async ({ newUser }) => {
    const owner = await newUser();
    const member = await newUser();
    const listId = await owner.createList("Groceries");
    await owner.addMember(member);
    await owner.addItem("Milk");
    await owner.addItem("Eggs");
    await member.openList(listId);
    await owner.receivingLiveUpdates(listId);

    await owner.saveSettings({ hideAfterMinutes: 0 });
    await owner.toggle("Milk");

    for (const user of [owner, member]) {
      await expect(user.page.getByRole("checkbox", { name: "Milk", exact: true })).toHaveCount(0);
      await expect(user.page.getByText("1 done item is hidden.")).toBeVisible();
    }

    await owner.page.reload();
    await expect(owner.page.getByText("Done items are hidden right away.")).toBeVisible();
    await expect(owner.page.getByRole("checkbox", { name: "Milk", exact: true })).toHaveCount(0);
    await owner.page.getByRole("button", { name: "Show done items" }).click();
    await expect(owner.page.getByRole("checkbox", { name: "Milk", exact: true })).toBeChecked();
  });

  test("says when an all-done list will be deleted, from the server's times", async ({
    newUser,
  }) => {
    const user = await newUser();
    const { page } = user;
    await user.createList("Move house");
    await user.addItem("Book van");
    await user.saveSettings({ deleteAfterDays: 1 });
    await expect(
      page.getByText("The list is deleted 1 day after every item is done."),
    ).toBeVisible();

    await user.toggle("Book van");
    await page.reload();

    const notice = page.getByRole("status").filter({ hasText: "will be deleted on" });
    // Completed just now plus one day, worked out from what the server stored.
    const tomorrow = await page.evaluate(() =>
      new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(Date.now() + 86_400_000),
    );
    await expect(notice).toContainText(
      `Every item is done, so this list will be deleted on ${tomorrow}`,
    );

    await user.toggle("Book van");
    await expect(notice).toHaveCount(0);
  });
});
