import type { Item, ItemGroup } from "../api/types";

export type ListState =
  | { status: "loading"; name: string | null }
  | { status: "error"; name: string | null; message: string; forbidden: boolean }
  | { status: "ready"; name: string; items: Item[]; members: string[] };

export type ListAction =
  | { type: "loading" }
  | { type: "loaded"; list: ItemGroup }
  | { type: "failed"; message: string; forbidden: boolean }
  | { type: "itemUpserted"; item: Item }
  | { type: "itemRemoved"; itemId: string }
  | { type: "renamed"; name: string }
  | { type: "memberAdded"; memberId: string }
  | { type: "memberRemoved"; memberId: string };

export function initialListState(name: string | null): ListState {
  return { status: "loading", name };
}

/**
 * Local changes and realtime events dispatch the same actions, so every
 * action is idempotent: applying it twice leaves the same state.
 */
export function listReducer(state: ListState, action: ListAction): ListState {
  switch (action.type) {
    case "loading":
      return { status: "loading", name: state.name };
    case "loaded":
      return {
        status: "ready",
        name: action.list.name,
        items: action.list.items,
        members: action.list.members,
      };
    case "failed":
      return {
        status: "error",
        name: state.name,
        message: action.message,
        forbidden: action.forbidden,
      };
  }

  if (state.status !== "ready") return state;

  switch (action.type) {
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
    case "memberAdded":
      return state.members.includes(action.memberId)
        ? state
        : { ...state, members: [...state.members, action.memberId] };
    case "memberRemoved":
      return { ...state, members: state.members.filter((id) => id !== action.memberId) };
  }
}

export type ItemFilter = "all" | "open" | "done";

export function filterItems(items: Item[], filter: ItemFilter, query: string): Item[] {
  const needle = query.trim().toLowerCase();
  return items.filter((item) => {
    if (filter === "open" && item.isComplete) return false;
    if (filter === "done" && !item.isComplete) return false;
    if (!needle) return true;
    return (
      item.name.toLowerCase().includes(needle) ||
      (item.description?.toLowerCase().includes(needle) ?? false)
    );
  });
}
