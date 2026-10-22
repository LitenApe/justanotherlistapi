import { runSql } from "./support/database";
import { expect, test } from "./support/test";

const DAY_SECONDS = 24 * 60 * 60;

test.describe("automatic deletion", () => {
  test("the server deletes an all-done list when its time comes and tells open pages", async ({
    newUser,
  }) => {
    // The cleanup service runs once a minute, so this takes up to about 90 seconds.
    test.setTimeout(180_000);
    const owner = await newUser();
    const member = await newUser();
    const listId = await owner.createList("Move house");
    await owner.addMember(member);
    await owner.addItem("Book van");
    await owner.saveSettings({ deleteAfterDays: 1 });
    await owner.toggle("Book van");

    // Auto-delete waits at least a day. Make it due 30 seconds from now, as if
    // everything happened a day ago; due later rather than already, so the pages
    // below are listening before the server acts.
    await runSql(`
      DECLARE @at DATETIMEOFFSET = DATEADD(second, ${(30 - DAY_SECONDS).toString()}, SYSDATETIMEOFFSET());
      UPDATE Items SET CompletedAt = @at WHERE ItemGroupId = '${listId}';
      UPDATE ItemGroups SET AutoDeleteUpdatedAt = @at WHERE Id = '${listId}';`);
    await owner.openList(listId);
    await member.openList(listId);
    await expect(owner.page.getByText(/so this list will be deleted on/)).toBeVisible();

    const message = "“Move house” was deleted automatically because every item was done.";
    for (const user of [owner, member]) {
      await expect(user.page.getByText(message)).toBeVisible({ timeout: 120_000 });
      await expect(user.page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    }
    await member.page.reload();
    await expect(member.page.getByRole("heading", { name: "No lists yet" })).toBeVisible();
  });
});
