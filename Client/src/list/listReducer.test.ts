import { describe, expect, it } from "vitest";
import type { Item, ItemGroupSettings } from "../api/types";
import { makeItem, makeList, OTHER } from "../test/fakeApi";
import {
  autoDeleteTime,
  filterItems,
  hideTime,
  isAutoHidden,
  listReducer,
  listStateFrom,
  nextHideTime,
} from "./listReducer";

const ready = () => listStateFrom(makeList("Groceries", ["Milk", "Eggs"]));

describe("listReducer", () => {
  it("replaces everything when the loader returns fresh data", () => {
    const fresh = makeList("Weekly shop", ["Bread"]);
    const next = listReducer(ready(), { type: "loaded", list: fresh });
    expect(next).toEqual(listStateFrom(fresh));
  });

  it("adds new items to the top and is idempotent", () => {
    const state = ready();
    const item = makeItem({ name: "Bread", itemGroupId: "g" });
    const once = listReducer(state, { type: "itemUpserted", item });
    const twice = listReducer(once, { type: "itemUpserted", item });

    expect(once.items.map((i) => i.name)).toEqual(["Bread", "Milk", "Eggs"]);
    expect(twice).toEqual(once);
  });

  it("updates existing items in place", () => {
    const state = ready();
    const [, eggs] = state.items;
    if (!eggs) throw new Error("missing item");
    const next = listReducer(state, { type: "itemUpserted", item: { ...eggs, isComplete: true } });

    expect(next.items.map((i) => [i.name, i.isComplete])).toEqual([
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

    expect(next.items.map((i) => i.name)).toEqual(["Eggs"]);
    expect(next.members).toHaveLength(2);

    next = listReducer(next, { type: "memberRemoved", memberId: OTHER });
    expect(next.members).toHaveLength(1);
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

  it("leaves hidden items out of All but keeps them under Done", () => {
    const isHidden = (item: Item) => item.name === "Eggs";
    expect(filterItems(items, "all", "", isHidden).map((i) => i.name)).toEqual(["Milk"]);
    expect(filterItems(items, "done", "", isHidden).map((i) => i.name)).toEqual(["Eggs"]);
  });
});

describe("settings", () => {
  const MINUTE = 60_000;
  const t0 = Date.parse("2026-10-19T12:00:00.000Z");
  const at = (minutes: number) => new Date(t0 + minutes * MINUTE).toISOString();
  const done = (name: string, completedAtMinute: number) =>
    makeItem({ name, itemGroupId: "g", isComplete: true, completedAt: at(completedAtMinute) });
  const open = (name: string) => makeItem({ name, itemGroupId: "g" });
  const settings = (overrides: Partial<ItemGroupSettings>): ItemGroupSettings => ({
    autoDeleteAfterMinutes: null,
    hideCompletedAfterMinutes: null,
    autoDeleteUpdatedAt: null,
    ...overrides,
  });

  it("replaces settings idempotently", () => {
    const changed = settings({ hideCompletedAfterMinutes: 30 });
    const once = listReducer(ready(), { type: "settingsChanged", settings: changed });
    const twice = listReducer(once, { type: "settingsChanged", settings: changed });

    expect(once.settings).toEqual(changed);
    expect(twice).toEqual(once);
  });

  it("hides done items once the delay has passed since they were completed", () => {
    const milk = done("Milk", 0);
    expect(hideTime(milk, 60)).toBe(t0 + 60 * MINUTE);
    expect(isAutoHidden(milk, 60, t0 + 59 * MINUTE)).toBe(false);
    expect(isAutoHidden(milk, 60, t0 + 60 * MINUTE)).toBe(true);
    expect(isAutoHidden(milk, 0, t0)).toBe(true);
    expect(isAutoHidden(milk, null, t0 + 1000 * MINUTE)).toBe(false);
    expect(isAutoHidden(open("Eggs"), 0, t0)).toBe(false);
  });

  it("finds the next item to hide", () => {
    const items = [done("Milk", 0), done("Eggs", 30), open("Bread")];
    expect(nextHideTime(items, 60, t0)).toBe(t0 + 60 * MINUTE);
    expect(nextHideTime(items, 60, t0 + 60 * MINUTE)).toBe(t0 + 90 * MINUTE);
    expect(nextHideTime(items, 60, t0 + 90 * MINUTE)).toBeNull();
    expect(nextHideTime(items, null, t0)).toBeNull();
  });

  it("predicts auto-deletion only when every item is done", () => {
    const autoDelete = settings({ autoDeleteAfterMinutes: 1440, autoDeleteUpdatedAt: at(-5000) });

    expect(autoDeleteTime([done("Milk", 0), done("Eggs", 30)], autoDelete)).toBe(
      t0 + (30 + 1440) * MINUTE,
    );
    expect(autoDeleteTime([done("Milk", 0), open("Eggs")], autoDelete)).toBeNull();
    expect(autoDeleteTime([], autoDelete)).toBeNull();
    expect(autoDeleteTime([done("Milk", 0)], settings({}))).toBeNull();
  });

  it("starts the auto-delete countdown no earlier than the last settings change", () => {
    const recentlyChanged = settings({
      autoDeleteAfterMinutes: 1440,
      autoDeleteUpdatedAt: at(100),
    });
    expect(autoDeleteTime([done("Milk", 0)], recentlyChanged)).toBe(t0 + (100 + 1440) * MINUTE);
  });
});
