import { act, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { ItemGroup } from "../api/types";
import { installFakeApi, makeItem, makeList, ME, OTHER } from "../test/fakeApi";
import { latestConnection } from "../test/fakeConnection";
import { expectNoAxeViolations, renderApp, signIn } from "../test/render";

async function openList(list: ItemGroup) {
  const api = installFakeApi([list]);
  signIn();
  const view = renderApp(`/lists/${list.id}`);
  await screen.findByRole("heading", { level: 2, name: "Items" });
  return { api, ...view };
}

const itemsList = () => screen.getByRole("list", { name: "Items" });

describe("ListPage", () => {
  it("shows items, progress and members without axe violations", async () => {
    const list = makeList("Groceries", ["Milk", "Eggs"], [ME, OTHER]);
    const [milk] = list.items;
    if (milk) milk.description = "Oat milk";
    await openList(list);

    expect(screen.getByRole("heading", { level: 1, name: "Groceries" })).toBeInTheDocument();
    expect(document.title).toBe("Groceries – JustAnotherList");
    expect(screen.getByText("0 of 2 done")).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Milk" })).toHaveAccessibleDescription("Oat milk");
    expect(screen.getByRole("button", { name: "Edit “Milk”" })).toBeInTheDocument();

    const members = screen.getByRole("region", { name: /Members/ });
    expect(within(members).getByText("You")).toBeInTheDocument();
    expect(
      within(members).getByRole("button", { name: `Remove member ${OTHER}` }),
    ).toBeInTheDocument();

    await expectNoAxeViolations();
  });

  it("renders the list name from the overview while loading", () => {
    const list = makeList("Groceries", ["Milk"]);
    installFakeApi([list]);
    signIn();

    renderApp(`/lists/${list.id}`, { name: "Groceries" });

    expect(screen.getByRole("heading", { level: 1, name: "Groceries" })).toBeInTheDocument();
  });

  it("toggles an item and sends the full item to the API", async () => {
    const list = makeList("Groceries", ["Milk"]);
    const { api, user } = await openList(list);
    const checkbox = screen.getByRole("checkbox", { name: "Milk" });

    await user.click(checkbox);

    expect(checkbox).toBeChecked();
    expect(screen.getByText("1 of 1 done")).toBeInTheDocument();
    const [put] = api.callsTo("PUT", /\/api\/list\/.+\/.+/);
    expect(put?.body).toEqual({ name: "Milk", description: null, isComplete: true });
    expect(put?.headers.get("X-SignalR-Connection-Id")).toBe("test-connection");
  });

  it("reverts a toggle that the server rejects", async () => {
    const list = makeList("Groceries", ["Milk"]);
    const { api, user } = await openList(list);
    api.failNext("PUT", /\/api\/list\/.+\/.+/, 500);

    await user.click(screen.getByRole("checkbox", { name: "Milk" }));

    const message = await screen.findByText(/Couldn't update “Milk”/);
    expect(message.closest("[role=alert]")).not.toBeNull();
    expect(screen.getByRole("checkbox", { name: "Milk" })).not.toBeChecked();
  });

  it("moves focus to the next item when a toggled item leaves the filtered view", async () => {
    const list = makeList("Groceries", ["Milk", "Eggs", "Bread"]);
    const { user } = await openList(list);

    await user.click(screen.getByRole("radio", { name: /To do/ }));
    await user.click(screen.getByRole("checkbox", { name: "Eggs" }));

    expect(screen.queryByRole("checkbox", { name: "Eggs" })).not.toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Bread" })).toHaveFocus();
    expect(screen.getByText("Showing 2 of 3 items")).toBeInTheDocument();
  });

  it("adds items to the top of the list and keeps focus in the field", async () => {
    const list = makeList("Groceries", ["Milk"]);
    const { user } = await openList(list);
    const input = screen.getByLabelText("Add an item");

    await user.type(input, "Coffee{Enter}");

    await waitFor(() => {
      expect(within(itemsList()).getAllByRole("checkbox")[0]).toHaveAccessibleName("Coffee");
    });
    expect(input).toHaveValue("");
    expect(input).toHaveFocus();
  });

  it("edits an item in a dialog and returns focus afterwards", async () => {
    const list = makeList("Groceries", ["Milk"]);
    const { api, user } = await openList(list);
    const editButton = screen.getByRole("button", { name: "Edit “Milk”" });

    await user.click(editButton);
    const dialog = screen.getByRole("dialog", { name: "Edit item" });
    const name = within(dialog).getByLabelText("Name");
    expect(name).toHaveFocus();
    await expectNoAxeViolations(dialog);

    await user.clear(name);
    await user.type(name, "Oat milk");
    await user.type(within(dialog).getByLabelText(/Notes/), "Barista edition");
    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await screen.findByRole("checkbox", { name: "Oat milk" })).toHaveAccessibleDescription(
      "Barista edition",
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(api.callsTo("PUT", /\/api\/list\/.+\/.+/)[0]?.body).toEqual({
      name: "Oat milk",
      description: "Barista edition",
      isComplete: false,
    });
  });

  it("confirms before deleting an item, then focuses its neighbour", async () => {
    const list = makeList("Groceries", ["Milk", "Eggs"]);
    const { api, user } = await openList(list);

    await user.click(screen.getByRole("button", { name: "Edit “Milk”" }));
    await user.click(screen.getByRole("button", { name: "Delete item" }));
    const confirm = screen.getByRole("dialog", { name: "Delete “Milk”?" });
    expect(within(confirm).getByRole("button", { name: "Cancel" })).toHaveFocus();

    await user.click(within(confirm).getByRole("button", { name: "Delete item" }));

    await waitFor(() => {
      expect(screen.queryByRole("checkbox", { name: "Milk" })).not.toBeInTheDocument();
    });
    expect(screen.getByRole("checkbox", { name: "Eggs" })).toHaveFocus();
    expect(api.callsTo("DELETE", /\/api\/list\/.+\/.+/)).toHaveLength(1);
  });

  it("renames the list", async () => {
    const list = makeList("Groceries");
    const { user } = await openList(list);

    await user.click(screen.getByRole("button", { name: "Rename" }));
    const input = within(screen.getByRole("dialog", { name: "Rename list" })).getByLabelText(
      "List name",
    );
    await user.clear(input);
    await user.type(input, "Weekly shop{Enter}");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Weekly shop" }),
    ).toBeInTheDocument();
  });

  it("deletes the list after confirmation and returns to the overview", async () => {
    const list = makeList("Groceries", ["Milk"]);
    const { user } = await openList(list);

    await user.click(screen.getByRole("button", { name: "Delete list" }));
    const confirm = screen.getByRole("dialog", { name: "Delete “Groceries”?" });
    expect(confirm).toHaveAccessibleDescription(/its 1 item for every member/);
    await user.click(within(confirm).getByRole("button", { name: "Delete list" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Your lists" })).toHaveFocus();
    expect(screen.getByText("Deleted “Groceries”.")).toBeInTheDocument();
  });

  it("adds members by user ID and explains conflicts", async () => {
    const list = makeList("Groceries", [], [ME, OTHER]);
    const { user } = await openList(list);
    const input = screen.getByLabelText("Add a member");

    await user.type(input, OTHER);
    await user.click(screen.getByRole("button", { name: "Add member" }));
    expect(input).toHaveAccessibleDescription(/That person is already a member/);

    const newMember = "00000000-0000-0000-0000-0000000000AB";
    await user.clear(input);
    await user.type(input, newMember);
    await user.click(screen.getByRole("button", { name: "Add member" }));

    expect(await screen.findByText(newMember.toLowerCase())).toBeInTheDocument();
    expect(input).toHaveValue("");
  });

  it("only offers to leave when someone else remains a member", async () => {
    await openList(makeList("Solo"));
    expect(screen.queryByRole("button", { name: "Leave list" })).not.toBeInTheDocument();
  });

  it("applies realtime changes from other members", async () => {
    const list = makeList("Groceries", ["Milk", "Eggs"], [ME, OTHER]);
    await openList(list);
    const [milk, eggs] = list.items;
    if (!milk || !eggs) throw new Error("missing items");

    act(() => {
      const connection = latestConnection();
      connection.emit("ItemCreated", list.id, makeItem({ name: "Bread", itemGroupId: list.id }));
      connection.emit("ItemUpdated", list.id, { ...milk, isComplete: true });
      connection.emit("ItemDeleted", list.id, eggs.id);
      connection.emit("GroupRenamed", list.id, "Weekly shop");
    });

    expect(screen.getByRole("heading", { level: 1, name: "Weekly shop" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Bread" })).not.toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Milk" })).toBeChecked();
    expect(screen.queryByRole("checkbox", { name: "Eggs" })).not.toBeInTheDocument();
  });

  it("leaves the page when you are removed from the list", async () => {
    const list = makeList("Groceries", [], [ME, OTHER]);
    await openList(list);

    act(() => {
      latestConnection().emit("MemberRemoved", list.id, ME);
    });

    expect(await screen.findByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    expect(screen.getByText("You were removed from “Groceries”.")).toBeInTheDocument();
  });

  it("explains when you don't have access", async () => {
    installFakeApi([makeList("Secret", [], [OTHER])]);
    signIn();
    const list = makeList("Unknown");

    renderApp(`/lists/${list.id}`);

    expect(
      await screen.findByRole("heading", { name: "You can't open this list" }),
    ).toBeInTheDocument();
    await expectNoAxeViolations();
  });

  it("handles addresses that aren't lists", async () => {
    signIn();
    renderApp("/lists/not-a-guid");
    expect(screen.getByRole("heading", { level: 1, name: "List not found" })).toBeInTheDocument();
    await expectNoAxeViolations();
  });
});
