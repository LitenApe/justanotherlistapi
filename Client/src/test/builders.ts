import type { Item, ItemGroup, ItemGroupSettings } from "../api/types";

/** Plain test data, shared by unit, browser and integration tests. */

export const ME = "00000000-0000-0000-0000-000000000001";
export const OTHER = "00000000-0000-0000-0000-000000000002";

export const defaultSettings = (): ItemGroupSettings => ({
  autoDeleteAfterMinutes: null,
  hideCompletedAfterMinutes: null,
  autoDeleteUpdatedAt: null,
});

export function makeItem(overrides: Partial<Item> & Pick<Item, "name" | "itemGroupId">): Item {
  return {
    id: crypto.randomUUID(),
    description: null,
    isComplete: false,
    completedAt: overrides.isComplete ? new Date().toISOString() : null,
    ...overrides,
  };
}

export function makeList(name: string, itemNames: string[] = [], members = [ME]): ItemGroup {
  const id = crypto.randomUUID();
  return {
    id,
    name,
    members,
    items: itemNames.map((itemName) => makeItem({ name: itemName, itemGroupId: id })),
    settings: defaultSettings(),
  };
}

const MINUTE = 60_000;
export const minutes = (count: number) => count * MINUTE;
export const days = (count: number) => count * 24 * 60 * MINUTE;

/** An ISO timestamp `ms` milliseconds before now. */
export const ago = (ms: number) => new Date(Date.now() - ms).toISOString();
