import { describe, expect, it } from "vitest";
import { makeItem, makeList, OTHER } from "../test/fakeApi";
import { filterItems, initialListState, listReducer, type ListState } from "./listReducer";

function ready(): Extract<ListState, { status: "ready" }> {
  const state = listReducer(initialListState(null), {
    type: "loaded",
    list: makeList("Groceries", ["Milk", "Eggs"]),
  });
  if (state.status !== "ready") throw new Error("expected ready");
  return state;
}

describe("listReducer", () => {
  it("keeps the name passed from the overview while loading", () => {
    expect(initialListState("Groceries")).toEqual({ status: "loading", name: "Groceries" });
  });

  it("adds new items to the top and is idempotent", () => {
    const state = ready();
    const item = makeItem({ name: "Bread", itemGroupId: "g" });
    const once = listReducer(state, { type: "itemUpserted", item });
    const twice = listReducer(once, { type: "itemUpserted", item });

    expect(once.status === "ready" && once.items.map((i) => i.name)).toEqual([
      "Bread",
      "Milk",
      "Eggs",
    ]);
    expect(twice).toEqual(once);
  });

  it("updates existing items in place", () => {
    const state = ready();
    const [, eggs] = state.items;
    if (!eggs) throw new Error("missing item");
    const next = listReducer(state, { type: "itemUpserted", item: { ...eggs, isComplete: true } });

    expect(next.status === "ready" && next.items.map((i) => [i.name, i.isComplete])).toEqual([
      ["Milk", false],
      ["Eggs", true],
    ]);
  });

  it("removes items and members idempotently", () => {
    const state = ready();
    const [milk] = state.items;
    if (!milk) throw new Error("missing item");
    let next = listReducer(state, { type: "itemRemoved", itemId: milk.id });
    next = listReducer(next, { type: "itemRemoved", itemId: milk.id });
    next = listReducer(next, { type: "memberAdded", memberId: OTHER });
    next = listReducer(next, { type: "memberAdded", memberId: OTHER });

    expect(next.status === "ready" && next.items.map((i) => i.name)).toEqual(["Eggs"]);
    expect(next.status === "ready" && next.members).toHaveLength(2);

    next = listReducer(next, { type: "memberRemoved", memberId: OTHER });
    expect(next.status === "ready" && next.members).toHaveLength(1);
  });

  it("ignores item events until the list has loaded", () => {
    const loading = initialListState(null);
    const next = listReducer(loading, { type: "renamed", name: "Ignored" });
    expect(next).toBe(loading);
  });
});

describe("filterItems", () => {
  const items = [
    makeItem({ name: "Milk", itemGroupId: "g", description: "Oat, 2 litres" }),
    makeItem({ name: "Eggs", itemGroupId: "g", isComplete: true }),
  ];

  it("filters by completion", () => {
    expect(filterItems(items, "open", "").map((i) => i.name)).toEqual(["Milk"]);
    expect(filterItems(items, "done", "").map((i) => i.name)).toEqual(["Eggs"]);
    expect(filterItems(items, "all", "")).toHaveLength(2);
  });

  it("searches names and descriptions, ignoring case", () => {
    expect(filterItems(items, "all", "EGG").map((i) => i.name)).toEqual(["Eggs"]);
    expect(filterItems(items, "all", "oat").map((i) => i.name)).toEqual(["Milk"]);
  });
});
