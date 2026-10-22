import { expect, test, type User } from "./support/test";

/** The owner creates a list and adds the member; both end up on the list page. */
async function sharedList(newUser: () => Promise<User>) {
  const owner = await newUser();
  const member = await newUser();
  const listId = await owner.createList("Groceries");
  await owner.addMember(member);
  await owner.receivingLiveUpdates(listId);
  await member.openList(listId);
  return { owner, member, listId };
}

test.describe("working together", () => {
  test("a shared list appears for the new member", async ({ newUser }) => {
    const owner = await newUser();
    const member = await newUser();
    await owner.createList("Groceries");

    await owner.addMember(member);
    // The member's overview loaded before the list was shared; coming back picks it up.
    await member.page.reload();

    await member.page.getByRole("link", { name: "Groceries" }).click();
    await expect(member.page.getByRole("heading", { level: 1, name: "Groceries" })).toBeVisible();
    await expect(member.members().getByText(owner.id, { exact: true })).toBeVisible();
    await expect(member.members().getByText("You", { exact: true })).toBeVisible();
  });

  test("members see each other's changes as they happen", async ({ newUser }) => {
    const { owner, member } = await sharedList(newUser);

    await owner.addItem("Milk");
    await expect(member.page.getByRole("checkbox", { name: "Milk", exact: true })).toBeVisible();

    await member.toggle("Milk");
    await expect(owner.page.getByRole("checkbox", { name: "Milk", exact: true })).toBeChecked();
    await expect(owner.page.getByText("1 of 1 done")).toBeVisible();

    await member.page.getByRole("button", { name: "Edit “Milk”" }).click();
    const editor = member.page.getByRole("dialog", { name: "Edit item" });
    await editor.getByLabel("Name").fill("Oat milk");
    await editor.getByRole("button", { name: "Save" }).click();
    await expect(owner.page.getByRole("checkbox", { name: "Oat milk" })).toBeChecked();

    await owner.page.getByRole("button", { name: "Rename" }).click();
    const name = owner.page.getByRole("dialog", { name: "Rename list" }).getByLabel("List name");
    await name.fill("Weekly shop");
    await name.press("Enter");
    await expect(member.page.getByRole("heading", { level: 1, name: "Weekly shop" })).toBeVisible();
    await expect(member.page).toHaveTitle("Weekly shop – JustAnotherList");
  });

  test("your own changes aren't reported back to you as someone else's", async ({ newUser }) => {
    const { owner, member } = await sharedList(newUser);
    const request = owner.page.waitForRequest(
      (sent) => sent.method() === "PUT" && sent.url().endsWith("/settings"),
    );

    await owner.saveSettings({ hideAfterMinutes: 30 });

    // The change carries this tab's connection ID, so the server skips it when broadcasting.
    expect((await request).headers()["x-signalr-connection-id"]).toBeTruthy();
    await expect(member.page.getByText("Another member changed the list settings.")).toBeVisible();
    await expect(member.page.getByText("Done items are hidden after 30 minutes.")).toBeVisible();
    await expect(owner.page.getByText("Done items are hidden after 30 minutes.")).toBeVisible();
    await expect(owner.page.getByText("Another member changed the list settings.")).toHaveCount(0);
  });

  test("a removed member loses the list at once", async ({ newUser }) => {
    const { owner, member } = await sharedList(newUser);

    await owner
      .members()
      .getByRole("button", { name: `Remove member ${member.id}` })
      .click();
    await owner.page
      .getByRole("dialog", { name: "Remove member?" })
      .getByRole("button", { name: "Remove member" })
      .click();

    await expect(member.page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    await expect(member.page.getByText("You were removed from “Groceries”.")).toBeVisible();
    await expect(member.page.getByRole("link", { name: "Groceries" })).toHaveCount(0);
    await expect(owner.members().getByText(member.id, { exact: true })).toHaveCount(0);
  });

  test("a member who leaves disappears from the other members' view", async ({ newUser }) => {
    const { owner, member } = await sharedList(newUser);

    await member.page.getByRole("button", { name: "Leave list" }).click();
    await member.page
      .getByRole("dialog", { name: "Leave “Groceries”?" })
      .getByRole("button", { name: "Leave list" })
      .click();

    await expect(member.page.getByText("You left “Groceries”.")).toBeVisible();
    await expect(member.page.getByText(/You were removed/)).toHaveCount(0);
    await expect(owner.members().getByText(member.id, { exact: true })).toHaveCount(0);
    // The owner is the last member now, so they can't leave.
    await expect(owner.page.getByRole("button", { name: "Leave list" })).toHaveCount(0);
  });

  test("deleting a list takes everyone else back to their lists", async ({ newUser }) => {
    const { owner, member } = await sharedList(newUser);

    await owner.page.getByRole("button", { name: "Delete list" }).click();
    await owner.page.getByRole("dialog").getByRole("button", { name: "Delete list" }).click();

    await expect(owner.page.getByText("Deleted “Groceries”.")).toBeVisible();
    await expect(member.page.getByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    await expect(member.page.getByText("“Groceries” was deleted by another member.")).toBeVisible();
  });

  test("lists stay private to their members", async ({ newUser }) => {
    const owner = await newUser();
    const stranger = await newUser();
    const listId = await owner.createList("Diary");
    await owner.addItem("Secret plans");

    await stranger.page.goto(`/lists/${listId}`);

    await expect(
      stranger.page.getByRole("heading", { level: 1, name: "You can't open this list" }),
    ).toBeVisible();
    await expect(stranger.page.getByText("Secret plans")).toHaveCount(0);
    await stranger.page.goto("/");
    await expect(stranger.page.getByRole("heading", { name: "No lists yet" })).toBeVisible();
  });
});
