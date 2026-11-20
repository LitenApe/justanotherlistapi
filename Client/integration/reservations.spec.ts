import { expect, makeItem, makeList, ME, OTHER, test } from "./support/test";

/** A third member, whose user ID starts differently from ME's and OTHER's. */
const SAM = "5a3e9c71-2b4d-4e8f-9a01-b2c3d4e5f607";

/** A trip list where OTHER has the tent and the stove, one of them from ME. */
function tripList() {
  const list = makeList("Trip", [], [ME, OTHER, SAM]);
  list.items = [
    makeItem({ name: "Tent", itemGroupId: list.id, reservedFor: OTHER, reservedBy: OTHER }),
    makeItem({ name: "Stove", itemGroupId: list.id, reservedFor: OTHER, reservedBy: ME }),
    makeItem({ name: "Map", itemGroupId: list.id, reservedFor: ME, reservedBy: SAM }),
    makeItem({ name: "Gas", itemGroupId: list.id }),
  ];
  return list;
}

test.describe("reservations", () => {
  test("show who each item is reserved for, and by whom", async ({ page, app }) => {
    await app.openList(tripList());

    await expect(page.getByRole("checkbox", { name: "Tent" })).toHaveAccessibleDescription(
      "Reserved by 00000000…",
    );
    await expect(page.getByRole("checkbox", { name: "Stove" })).toHaveAccessibleDescription(
      "Reserved for 00000000… by you",
    );
    await expect(page.getByRole("checkbox", { name: "Map" })).toHaveAccessibleDescription(
      "Reserved for you by 5a3e9c71…",
    );
    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription("");
  });

  test("show reservations other members make as they happen", async ({ page, backend, app }) => {
    const list = tripList();
    await app.openList(list);
    const [tent, , , gas] = list.items;
    if (!tent || !gas) throw new Error("missing items");

    backend.as(SAM).reserveItem(list.id, gas.id, ME);
    backend.as(SAM).unreserveItem(list.id, tent.id);

    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription(
      "Reserved for you by 5a3e9c71…",
    );
    await expect(page.getByRole("checkbox", { name: "Tent" })).toHaveAccessibleDescription("");
  });

  test("free the items of a member another member removes", async ({ page, backend, app }) => {
    const list = tripList();
    await app.openList(list);

    backend.as(SAM).removeMember(list.id, OTHER);

    await expect(page.getByRole("checkbox", { name: "Tent" })).toHaveAccessibleDescription("");
    await expect(page.getByRole("checkbox", { name: "Stove" })).toHaveAccessibleDescription("");
    await expect(page.getByRole("checkbox", { name: "Map" })).toHaveAccessibleDescription(
      "Reserved for you by 5a3e9c71…",
    );
  });

  test("free the items of a member who leaves", async ({ page, backend, app }) => {
    const list = tripList();
    await app.openList(list);

    backend.as(SAM).removeMember(list.id, SAM);

    // The map stays reserved for you; who reserved it has gone.
    await expect(page.getByRole("checkbox", { name: "Map" })).toHaveAccessibleDescription(
      "Reserved for you",
    );
    backend.as(OTHER).removeMember(list.id, OTHER);
    await expect(page.getByRole("checkbox", { name: "Tent" })).toHaveAccessibleDescription("");
  });

  test("free the items of a member you remove", async ({ page, app }) => {
    await app.openList(tripList());

    await page.getByRole("button", { name: `Remove member ${OTHER}` }).click();
    await page
      .getByRole("dialog", { name: "Remove member?" })
      .getByRole("button", { name: "Remove member" })
      .click();

    await expect(page.getByRole("checkbox", { name: "Tent" })).toHaveAccessibleDescription("");
    await expect(page.getByRole("checkbox", { name: "Stove" })).toHaveAccessibleDescription("");
  });
});
