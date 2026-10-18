import { act, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { installFakeApi, makeItem, makeList } from "../test/fakeApi";
import { latestConnection } from "../test/fakeConnection";
import { expectNoAxeViolations, renderApp, signIn } from "../test/render";

describe("ListsPage", () => {
  it("lists your checklists alphabetically with what's left to do", async () => {
    const groceries = makeList("groceries", ["Milk", "Eggs", "Bread", "Butter"]);
    const garden = makeList("Garden", []);
    installFakeApi([groceries, garden]);
    signIn();

    renderApp("/");

    const cards = await screen.findAllByRole("link", { name: /garden|groceries/i });
    expect(cards.map((card) => card.textContent)).toEqual(["Garden", "groceries"]);
    expect(screen.getByRole("link", { name: "groceries" })).toHaveAccessibleDescription(
      /4 items to do.*Milk.*and 1 more/,
    );
    expect(screen.getByRole("link", { name: "Garden" })).toHaveAccessibleDescription("All done");
    await expectNoAxeViolations();
  });

  it("shows a friendly empty state", async () => {
    installFakeApi([]);
    signIn();

    renderApp("/");

    expect(await screen.findByRole("heading", { name: "No lists yet" })).toBeInTheDocument();
    await expectNoAxeViolations();
  });

  it("creates a list and opens it", async () => {
    const api = installFakeApi([]);
    signIn();
    const { user } = renderApp("/");
    await screen.findByRole("heading", { name: "No lists yet" });

    await user.type(screen.getByLabelText("New list"), "Holiday packing");
    await user.click(screen.getByRole("button", { name: "Create list" }));

    const heading = await screen.findByRole("heading", { level: 1, name: "Holiday packing" });
    // Navigation moves focus to the new page's heading.
    expect(heading).toHaveFocus();
    expect(api.callsTo("POST", /^\/api\/list$/)[0]?.body).toEqual({ name: "Holiday packing" });
  });

  it("requires a name before creating a list", async () => {
    const api = installFakeApi([]);
    signIn();
    const { user } = renderApp("/");
    await screen.findByRole("heading", { name: "No lists yet" });

    await user.click(screen.getByRole("button", { name: "Create list" }));

    const input = screen.getByLabelText("New list");
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Give your list a name.");
    expect(api.callsTo("POST", /^\/api\/list$/)).toHaveLength(0);
  });

  it("offers a retry when loading fails", async () => {
    const api = installFakeApi([makeList("Groceries")]);
    api.failNext("GET", /^\/api\/list$/, 500);
    signIn();
    const { user } = renderApp("/");

    const message = await screen.findByText(/Couldn't load your lists/);
    const alert = message.closest<HTMLElement>("[role=alert]");
    if (!alert) throw new Error("Expected the error to be announced as an alert.");
    await user.click(within(alert).getByRole("button", { name: "Try again" }));

    expect(await screen.findByRole("link", { name: "Groceries" })).toBeInTheDocument();
  });

  it("keeps cards current with realtime updates", async () => {
    const groceries = makeList("Groceries", ["Milk"]);
    const chores = makeList("Chores", ["Vacuum"]);
    installFakeApi([groceries, chores]);
    signIn();
    renderApp("/");
    await screen.findByRole("link", { name: "Groceries" });

    act(() => {
      const connection = latestConnection();
      connection.emit(
        "ItemCreated",
        groceries.id,
        makeItem({ name: "Eggs", itemGroupId: groceries.id }),
      );
      connection.emit("GroupRenamed", groceries.id, "Weekly shop");
      connection.emit("GroupDeleted", chores.id);
    });

    expect(screen.getByRole("link", { name: "Weekly shop" })).toHaveAccessibleDescription(
      /2 items to do/,
    );
    expect(screen.queryByRole("link", { name: "Chores" })).not.toBeInTheDocument();
  });

  it("filters long collections of lists", async () => {
    installFakeApi(Array.from({ length: 8 }, (_, i) => makeList(`List ${i + 1}`)));
    signIn();
    const { user } = renderApp("/");
    await screen.findByRole("link", { name: "List 1" });

    await user.type(screen.getByRole("searchbox", { name: "Find a list" }), "7");

    expect(screen.getAllByRole("link", { name: /^List/ })).toHaveLength(1);
    expect(screen.getByText("Showing 1 of 8 lists")).toBeInTheDocument();
  });
});
