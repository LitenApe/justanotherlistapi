import { act, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { ItemGroup } from "../api/types";
import { installFakeApi, makeItem, makeList, ME, OTHER } from "../test/fakeApi";
import { latestConnection, subscribedTo } from "../test/fakeConnection";
import { expectNoAxeViolations, renderApp, signIn } from "../test/render";

async function openList(list: ItemGroup) {
  const api = installFakeApi([list]);
  signIn();
  const view = renderApp(`/lists/${list.id}`);
  await screen.findByRole("heading", { level: 2, name: "Items" });
  await subscribedTo(list.id);
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

  it("announces loading until the list's data arrives", async () => {
    const list = makeList("Groceries", ["Milk"]);
    installFakeApi([list]);
    signIn();

    renderApp(`/lists/${list.id}`);

    expect(screen.getByRole("status")).toHaveTextContent("Loading JustAnotherList…");
    expect(await screen.findByRole("heading", { level: 1, name: "Groceries" })).toBeInTheDocument();
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

  it("offers a retry when the list fails to load", async () => {
    const list = makeList("Groceries", ["Milk"]);
    const api = installFakeApi([list]);
    api.failNext("GET", /^\/api\/list\/.+$/, 500);
    signIn();
    const { user } = renderApp(`/lists/${list.id}`);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Couldn't load this list" }),
    ).toBeInTheDocument();
    const alert = screen
      .getByText(/The server ran into a problem/)
      .closest<HTMLElement>("[role=alert]");
    if (!alert) throw new Error("Expected the error to be announced as an alert.");
    await expectNoAxeViolations();

    await user.click(within(alert).getByRole("button", { name: "Try again" }));

    expect(await screen.findByRole("heading", { level: 1, name: "Groceries" })).toBeInTheDocument();
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
    expect(
      await screen.findByRole("heading", { level: 1, name: "List not found" }),
    ).toBeInTheDocument();
    await expectNoAxeViolations();
  });
});

const MINUTE = 60_000;
const DAY = 24 * 60 * MINUTE;
const ago = (ms: number) => new Date(Date.now() - ms).toISOString();

describe("ListPage settings", () => {
  it("saves settings in a dialog and enforces the one-day auto-delete minimum", async () => {
    const { api, user } = await openList(makeList("Groceries", ["Milk"]));

    await user.click(screen.getByRole("button", { name: "Settings" }));
    const dialog = screen.getByRole("dialog", { name: "List settings" });
    expect(dialog).toHaveAccessibleDescription("These settings apply to everyone in this list.");
    const hide = within(dialog).getByRole("checkbox", { name: "Hide done items" });
    expect(hide).toHaveFocus();
    expect(hide).not.toBeChecked();

    await user.click(hide);
    const hideAfter = within(dialog).getByRole("group", { name: "Hide after" });
    const hideAmount = within(hideAfter).getByLabelText("Amount");
    expect(hideAmount).toHaveValue("1");
    expect(within(hideAfter).getByLabelText("Unit")).toHaveValue("days");
    await user.clear(hideAmount);
    await user.type(hideAmount, "2");
    await user.selectOptions(within(hideAfter).getByLabelText("Unit"), "hours");

    await user.click(
      within(dialog).getByRole("checkbox", { name: "Delete the list when every item is done" }),
    );
    const deleteAfter = within(dialog).getByRole("group", { name: "Delete after" });
    const deleteAmount = within(deleteAfter).getByLabelText("Amount");
    await user.clear(deleteAmount);
    await user.type(deleteAmount, "12");
    await user.selectOptions(within(deleteAfter).getByLabelText("Unit"), "hours");
    await expectNoAxeViolations(dialog);

    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(deleteAmount).toHaveFocus();
    expect(deleteAmount).toBeInvalid();
    expect(deleteAmount).toHaveAccessibleDescription(/Choose at least 1 day/);
    expect(api.callsTo("PUT", /\/settings$/)).toHaveLength(0);
    await expectNoAxeViolations(dialog);

    await user.selectOptions(within(deleteAfter).getByLabelText("Unit"), "days");
    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });
    expect(api.callsTo("PUT", /\/settings$/)[0]?.body).toEqual({
      hideCompletedAfterMinutes: 120,
      autoDeleteAfterMinutes: 12 * 24 * 60,
    });
    expect(
      screen.getByText(
        "Done items are hidden after 2 hours. The list is deleted 12 days after every item is done.",
      ),
    ).toBeInTheDocument();
  });

  it("hides done items after the chosen time but keeps them under Done", async () => {
    const list = makeList("Groceries");
    list.settings.hideCompletedAfterMinutes = 60;
    list.items = [
      makeItem({ name: "Milk", itemGroupId: list.id, isComplete: true, completedAt: ago(DAY) }),
      makeItem({ name: "Eggs", itemGroupId: list.id, isComplete: true }),
      makeItem({ name: "Bread", itemGroupId: list.id }),
    ];
    const { user } = await openList(list);

    expect(screen.queryByRole("checkbox", { name: "Milk" })).not.toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Eggs" })).toBeChecked();
    expect(screen.getByText("2 of 3 done")).toBeInTheDocument();
    expect(screen.getByText("Showing 2 of 3 items")).toBeInTheDocument();
    expect(screen.getByText(/1 done item is hidden/)).toBeInTheDocument();
    expect(screen.getByText("Done items are hidden after 1 hour.")).toBeInTheDocument();
    await expectNoAxeViolations();

    await user.click(screen.getByRole("button", { name: "Show done items" }));

    const done = screen.getByRole("radio", { name: /^Done/ });
    expect(done).toBeChecked();
    expect(done).toHaveFocus();
    expect(screen.getByRole("checkbox", { name: "Milk" })).toBeChecked();
  });

  it("hides an item as soon as it's checked when set to hide right away", async () => {
    const list = makeList("Groceries", ["Milk", "Eggs"]);
    list.settings.hideCompletedAfterMinutes = 0;
    const { api, user } = await openList(list);

    await user.click(screen.getByRole("checkbox", { name: "Milk" }));

    expect(screen.queryByRole("checkbox", { name: "Milk" })).not.toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Eggs" })).toHaveFocus();
    expect(screen.getByText(/1 done item is hidden/)).toBeInTheDocument();
    expect(api.callsTo("PUT", /\/api\/list\/.+\/.+/)[0]?.body).toEqual({
      name: "Milk",
      description: null,
      isComplete: true,
    });
  });

  it("hides a done item when its time comes and moves focus off it", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ["setTimeout", "clearTimeout", "Date"] });
    try {
      const list = makeList("Groceries");
      list.settings.hideCompletedAfterMinutes = 1;
      list.items = [
        makeItem({ name: "Milk", itemGroupId: list.id, isComplete: true }),
        makeItem({ name: "Eggs", itemGroupId: list.id }),
      ];
      await openList(list);
      // Let React run the effect that schedules the hide timer before moving the clock.
      await act(() => Promise.resolve());
      act(() => {
        screen.getByRole("checkbox", { name: "Milk" }).focus();
      });

      act(() => {
        vi.advanceTimersByTime(MINUTE);
      });

      expect(screen.queryByRole("checkbox", { name: "Milk" })).not.toBeInTheDocument();
      expect(screen.getByRole("checkbox", { name: "Eggs" })).toHaveFocus();
    } finally {
      vi.useRealTimers();
    }
  });

  it("says when the list will be deleted once every item is done", async () => {
    const list = makeList("Groceries");
    list.settings = {
      autoDeleteAfterMinutes: 24 * 60,
      hideCompletedAfterMinutes: null,
      autoDeleteUpdatedAt: ago(7 * DAY),
    };
    list.items = [
      makeItem({ name: "Milk", itemGroupId: list.id, isComplete: true }),
      makeItem({ name: "Eggs", itemGroupId: list.id }),
    ];
    const { user } = await openList(list);
    expect(
      screen.getByText("The list is deleted 1 day after every item is done."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/will be deleted on/)).not.toBeInTheDocument();

    await user.click(screen.getByRole("checkbox", { name: "Eggs" }));

    const notice = screen.getByText(/Every item is done, so this list will be deleted on/);
    expect(notice.closest("[role=status]")).not.toBeNull();
    await expectNoAxeViolations();

    await user.click(screen.getByRole("checkbox", { name: "Eggs" }));
    expect(screen.queryByText(/will be deleted on/)).not.toBeInTheDocument();
  });

  it("applies settings changed by other members and explains automatic deletion", async () => {
    const list = makeList("Groceries");
    list.items = [
      makeItem({ name: "Milk", itemGroupId: list.id, isComplete: true, completedAt: ago(3 * DAY) }),
    ];
    await openList(list);

    act(() => {
      latestConnection().emit("GroupSettingsChanged", list.id, {
        autoDeleteAfterMinutes: 24 * 60,
        hideCompletedAfterMinutes: null,
        autoDeleteUpdatedAt: ago(2 * DAY),
      });
    });

    expect(screen.getByText("Another member changed the list settings.")).toBeInTheDocument();
    expect(screen.getByText(/will be deleted on/)).toBeInTheDocument();

    act(() => {
      latestConnection().emit("GroupDeleted", list.id);
    });

    expect(await screen.findByRole("heading", { level: 1, name: "Your lists" })).toBeVisible();
    expect(
      screen.getByText("“Groceries” was deleted automatically because every item was done."),
    ).toBeInTheDocument();
  });
});
