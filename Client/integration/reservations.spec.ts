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

  test("reserve a free item for yourself in two clicks", async ({ page, backend, app }) => {
    const list = tripList();
    await app.openList(list);

    await page.getByRole("button", { name: "Reserve “Gas”" }).click();
    const dialog = page.getByRole("dialog", { name: "Reserve “Gas”" });
    await expect(dialog.getByRole("radio", { name: "You" })).toBeFocused();
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(dialog).toBeHidden();
    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription(
      "Reserved by you",
    );
    await expect(page.getByRole("button", { name: "Change reservation for “Gas”" })).toBeFocused();
    const gas = backend.list(list.id)?.items.find((item) => item.name === "Gas");
    expect(gas).toMatchObject({ reservedFor: ME, reservedBy: ME });
  });

  test("reserve an item for another member, then cancel it", async ({ page, app }) => {
    await app.openList(tripList());

    await page.getByRole("button", { name: "Reserve “Gas”" }).click();
    const dialog = page.getByRole("dialog", { name: "Reserve “Gas”" });
    await dialog.getByRole("radio", { name: SAM }).check();
    await dialog.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription(
      "Reserved for 5a3e9c71… by you",
    );

    await page.getByRole("button", { name: "Change reservation for “Gas”" }).click();
    await dialog.getByRole("radio", { name: "Nobody" }).check();
    await dialog.getByRole("button", { name: "Save" }).click();
    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription("");
  });

  test("take over another member's reservation in a list that doesn't protect them", async ({
    page,
    backend,
    app,
  }) => {
    const list = tripList();
    await app.openList(list);

    await page.getByRole("button", { name: "Change reservation for “Tent”" }).click();
    const dialog = page.getByRole("dialog", { name: "Reserve “Tent”" });
    await expect(dialog.getByText("Reserved by 00000000….")).toBeVisible();
    await dialog.getByRole("radio", { name: "You" }).check();
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(page.getByRole("checkbox", { name: "Tent" })).toHaveAccessibleDescription(
      "Reserved by you",
    );
    const call = backend.calls.find((c) => c.method === "PUT" && c.path.endsWith("/reservation"));
    expect(call?.body).toEqual({ memberId: ME, replacing: OTHER });
  });

  test("leave other people's reservations alone in a list that protects them", async ({
    page,
    app,
  }) => {
    const list = tripList();
    list.settings.protectReservations = true;
    await app.openList(list);

    // OTHER reserved the tent for themselves; ME reserved the stove for OTHER.
    await expect(page.getByRole("button", { name: "Change reservation for “Tent”" })).toHaveCount(
      0,
    );
    await expect(
      page.getByRole("button", { name: "Change reservation for “Stove”" }),
    ).toBeVisible();
    await expect(page.getByRole("button", { name: "Change reservation for “Map”" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Reserve “Gas”" })).toBeVisible();
  });

  test("say so when someone else reserved the item first", async ({ page, backend, app }) => {
    const list = tripList();
    await app.openList(list);
    const gas = list.items[3];
    if (!gas) throw new Error("missing item");

    await page.getByRole("button", { name: "Reserve “Gas”" }).click();
    const dialog = page.getByRole("dialog", { name: "Reserve “Gas”" });
    backend.as(SAM).reserveItem(list.id, gas.id, SAM);
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(dialog).toBeHidden();
    await expect(
      page.getByRole("alert").filter({ hasText: "Someone changed the reservation of “Gas”" }),
    ).toBeVisible();
    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription(
      "Reserved by 5a3e9c71…",
    );
    await expect(page.getByRole("checkbox", { name: "Gas" })).toBeFocused();
    expect(backend.list(list.id)?.items[3]).toMatchObject({ reservedFor: SAM });
  });

  test("keep a reservation made while your edit was on its way", async ({ page, backend, app }) => {
    const list = tripList();
    await app.openList(list);
    const gas = list.items[3];
    if (!gas) throw new Error("missing item");
    const release = backend.holdNext("PUT", new RegExp(`/api/list/${list.id}/${gas.id}$`));

    await page.getByRole("button", { name: "Edit “Gas”" }).click();
    await page.getByLabel("Name").fill("Gas canister");
    await page.getByRole("button", { name: "Save" }).click();
    backend.as(SAM).reserveItem(list.id, gas.id, SAM);
    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription(
      "Reserved by 5a3e9c71…",
    );
    release();

    await expect(page.getByRole("checkbox", { name: "Gas canister" })).toHaveAccessibleDescription(
      "Reserved by 5a3e9c71…",
    );
  });

  test("keep a reservation made while a failed toggle was on its way", async ({
    page,
    backend,
    app,
  }) => {
    const list = tripList();
    await app.openList(list);
    const gas = list.items[3];
    if (!gas) throw new Error("missing item");
    const pattern = new RegExp(`/api/list/${list.id}/${gas.id}$`);
    const release = backend.holdNext("PUT", pattern);
    backend.failNext("PUT", pattern, 500);

    // click, not check(): the server rejects it, so the box flips back.
    await page.getByRole("checkbox", { name: "Gas" }).click();
    backend.as(SAM).reserveItem(list.id, gas.id, SAM);
    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription(
      "Reserved by 5a3e9c71…",
    );
    release();

    await expect(
      page.getByRole("alert").filter({ hasText: "Couldn't update “Gas”" }),
    ).toBeVisible();
    await expect(page.getByRole("checkbox", { name: "Gas" })).not.toBeChecked();
    await expect(page.getByRole("checkbox", { name: "Gas" })).toHaveAccessibleDescription(
      "Reserved by 5a3e9c71…",
    );
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
