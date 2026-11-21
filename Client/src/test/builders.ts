import type { Item, ItemGroup, ItemGroupSettings, Member } from "../api/types";

/** Plain test data, shared by unit, browser and integration tests. */

export const ME = "00000000-0000-0000-0000-000000000001";
export const OTHER = "00000000-0000-0000-0000-000000000002";

/** The names of the people tests know: ME is Robin, OTHER is Kim. */
const KNOWN_NAMES: Partial<Record<string, string>> = { [ME]: "Robin", [OTHER]: "Kim" };

/** A member of a list, called by their known name unless told otherwise. */
export const member = (id: string, name: string | null = KNOWN_NAMES[id] ?? null): Member => ({
  id,
  name,
});

export const defaultSettings = (): ItemGroupSettings => ({
  autoDeleteAfterMinutes: null,
  hideCompletedAfterMinutes: null,
  autoDeleteUpdatedAt: null,
  protectReservations: false,
});

export function makeItem(overrides: Partial<Item> & Pick<Item, "name" | "itemGroupId">): Item {
  return {
    id: crypto.randomUUID(),
    description: null,
    isComplete: false,
    completedAt: overrides.isComplete ? new Date().toISOString() : null,
    reservedFor: null,
    reservedBy: null,
    ...overrides,
  };
}

/** A list; members can be given as user IDs, for people tests know by name, or as members. */
export function makeList(
  name: string,
  itemNames: string[] = [],
  members: readonly (string | Member)[] = [ME],
): ItemGroup {
  const id = crypto.randomUUID();
  return {
    id,
    name,
    members: members.map((entry) => (typeof entry === "string" ? member(entry) : entry)),
    items: itemNames.map((itemName) => makeItem({ name: itemName, itemGroupId: id })),
    settings: defaultSettings(),
  };
}

const MINUTE = 60_000;
export const minutes = (count: number) => count * MINUTE;
export const days = (count: number) => count * 24 * 60 * MINUTE;

/** An ISO timestamp `ms` milliseconds before now. */
export const ago = (ms: number) => new Date(Date.now() - ms).toISOString();
