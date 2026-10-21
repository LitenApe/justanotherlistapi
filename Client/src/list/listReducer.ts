import type { Item, ItemGroup, ItemGroupSettings } from "../api/types";

/** A loaded list. Loading and load errors belong to the route (clientLoader, ErrorBoundary). */
export interface ListState {
  name: string;
  items: Item[];
  members: string[];
  settings: ItemGroupSettings;
}

export type ListAction =
  | { type: "loaded"; list: ItemGroup }
  | { type: "itemUpserted"; item: Item }
  | { type: "itemRemoved"; itemId: string }
  | { type: "renamed"; name: string }
  | { type: "settingsChanged"; settings: ItemGroupSettings }
  | { type: "memberAdded"; memberId: string }
  | { type: "memberRemoved"; memberId: string };

export function listStateFrom(list: ItemGroup): ListState {
  return { name: list.name, items: list.items, members: list.members, settings: list.settings };
}

/**
 * Local changes and realtime events dispatch the same actions, so every
 * action is idempotent: applying it twice leaves the same state.
 */
export function listReducer(state: ListState, action: ListAction): ListState {
  switch (action.type) {
    case "loaded":
      return listStateFrom(action.list);
    case "itemUpserted": {
      const index = state.items.findIndex((item) => item.id === action.item.id);
      // New items go to the top, next to the form that adds them.
      const items =
        index === -1 ? [action.item, ...state.items] : state.items.with(index, action.item);
      return { ...state, items };
    }
    case "itemRemoved":
      return { ...state, items: state.items.filter((item) => item.id !== action.itemId) };
    case "renamed":
      return { ...state, name: action.name };
    case "settingsChanged":
      return { ...state, settings: action.settings };
    case "memberAdded":
      return state.members.includes(action.memberId)
        ? state
        : { ...state, members: [...state.members, action.memberId] };
    case "memberRemoved":
      return { ...state, members: state.members.filter((id) => id !== action.memberId) };
  }
}

export type ItemFilter = "all" | "open" | "done";

/**
 * Auto-hidden items drop out of "All" (and "To do", which has no done items
 * anyway). "Done" still lists them, so they can be found and unchecked.
 */
export function filterItems(
  items: Item[],
  filter: ItemFilter,
  query: string,
  isHidden: (item: Item) => boolean = () => false,
): Item[] {
  const needle = query.trim().toLowerCase();
  return items.filter((item) => {
    if (filter === "open" && item.isComplete) return false;
    if (filter === "done" && !item.isComplete) return false;
    if (filter !== "done" && isHidden(item)) return false;
    if (!needle) return true;
    return (
      item.name.toLowerCase().includes(needle) ||
      (item.description?.toLowerCase().includes(needle) ?? false)
    );
  });
}

const MINUTE = 60_000;

/** When a done item gets hidden, or null if it never will (auto-hide off, or the item is open). */
export function hideTime(item: Item, hideAfterMinutes: number | null): number | null {
  if (hideAfterMinutes === null || !item.isComplete || item.completedAt === null) return null;
  return Date.parse(item.completedAt) + hideAfterMinutes * MINUTE;
}

export function isAutoHidden(item: Item, hideAfterMinutes: number | null, now: number): boolean {
  const at = hideTime(item, hideAfterMinutes);
  return at !== null && at <= now;
}

/** The next moment a still-visible done item gets hidden, or null if none will. */
export function nextHideTime(
  items: Item[],
  hideAfterMinutes: number | null,
  now: number,
): number | null {
  let next: number | null = null;
  for (const item of items) {
    const at = hideTime(item, hideAfterMinutes);
    if (at !== null && at > now && (next === null || at < next)) next = at;
  }
  return next;
}

/**
 * When the server will delete the list: the auto-delete delay after the later of
 * the last completion and the last change to the setting (mirrors the server's
 * AutoDeleteItemGroups). Null unless auto-delete is on and every item is done.
 */
export function autoDeleteTime(items: Item[], settings: ItemGroupSettings): number | null {
  const minutes = settings.autoDeleteAfterMinutes;
  if (minutes === null || items.length === 0 || items.some((item) => !item.isComplete)) {
    return null;
  }
  const starts = [...items.map((item) => item.completedAt), settings.autoDeleteUpdatedAt]
    .filter((time) => time !== null)
    .map((time) => Date.parse(time));
  return starts.length > 0 ? Math.max(...starts) + minutes * MINUTE : null;
}
