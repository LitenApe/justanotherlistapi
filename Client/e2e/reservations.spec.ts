import { expect, test, type User } from "./support/test";

/** How other members see a user in a reservation: the start of their user ID. */
const label = (user: User) => `${user.id.slice(0, 8)}…`;

test.describe("reservations", () => {
  test("members reserve items for themselves and each other, live and for good", async ({
    newUser,
  }) => {
    const alice = await newUser();
    const bob = await newUser();
    const listId = await alice.createList("Trip");
    await alice.addItem("Tent");
    await alice.addItem("Stove");
    await alice.addMember(bob);
    await bob.openList(listId);
    await alice.receivingLiveUpdates(listId);

    await alice.reserve("Tent", alice);
    await expect(bob.item("Tent")).toHaveAccessibleDescription(`Reserved by ${label(alice)}`);
    await alice.reserve("Stove", bob);
    await expect(bob.item("Stove")).toHaveAccessibleDescription(
      `Reserved for you by ${label(alice)}`,
    );

    // The list doesn't protect reservations, so Bob can take the tent over.
    await bob.reserve("Tent", bob);
    await expect(alice.item("Tent")).toHaveAccessibleDescription(`Reserved by ${label(bob)}`);

    await alice.page.reload();
    await expect(alice.item("Tent")).toHaveAccessibleDescription(`Reserved by ${label(bob)}`);
    await expect(alice.item("Stove")).toHaveAccessibleDescription(
      `Reserved for ${label(bob)} by you`,
    );
  });

  test("a list that protects reservations keeps them from other members", async ({ newUser }) => {
    const alice = await newUser();
    const bob = await newUser();
    const listId = await alice.createList("Work");
    await alice.addItem("Report");
    await alice.addItem("Slides");
    await alice.addMember(bob);
    await alice.saveSettings({ protectReservations: true });
    await alice.reserve("Report", alice);
    await bob.openList(listId);

    await expect(bob.item("Report")).toHaveAccessibleDescription(`Reserved by ${label(alice)}`);
    await expect(
      bob.page.getByRole("button", { name: "Change reservation for “Report”" }),
    ).toHaveCount(0);
    // Free items are still anyone's to reserve.
    await bob.reserve("Slides", bob);
    await expect(bob.item("Slides")).toHaveAccessibleDescription("Reserved by you");
  });

  test("items reserved for a member are free again when they leave or are removed", async ({
    newUser,
  }) => {
    const alice = await newUser();
    const bob = await newUser();
    const carol = await newUser();
    const listId = await alice.createList("Trip");
    await alice.addItem("Tent");
    await alice.addItem("Stove");
    await alice.addMember(bob);
    await alice.addMember(carol);
    await alice.reserve("Stove", bob);
    await carol.openList(listId);
    await alice.receivingLiveUpdates(listId);
    await carol.reserve("Tent", carol);
    await expect(alice.item("Tent")).toHaveAccessibleDescription(`Reserved by ${label(carol)}`);

    // Carol leaves the list.
    await carol.page.getByRole("button", { name: "Leave list" }).click();
    await carol.page
      .getByRole("dialog", { name: "Leave “Trip”?" })
      .getByRole("button", { name: "Leave list" })
      .click();
    await expect(alice.item("Tent")).toHaveAccessibleDescription("");

    // Alice removes Bob.
    await alice.page.getByRole("button", { name: `Remove member ${bob.id}` }).click();
    await alice.page
      .getByRole("dialog", { name: "Remove member?" })
      .getByRole("button", { name: "Remove member" })
      .click();
    await expect(alice.item("Stove")).toHaveAccessibleDescription("");

    // The server freed them too.
    await alice.page.reload();
    await expect(alice.item("Tent")).toHaveAccessibleDescription("");
    await expect(alice.item("Stove")).toHaveAccessibleDescription("");
  });
});
