import { useCallback, useOptimistic } from "react";

import type { Item } from "@shared/types";
import { deleteItem, toggleItem } from "./api";
import { invalidateChecklists } from "../checklists";
import { useTrackedTransition } from "@shared/hooks";

type ItemAction =
  | { type: "toggle"; id: string }
  | { type: "remove"; id: string };

function reducer(items: Item[], action: ItemAction): Item[] {
  switch (action.type) {
    case "toggle":
      return items.map((item) =>
        item.id === action.id
          ? { ...item, isComplete: !item.isComplete }
          : item,
      );
    case "remove":
      return items.filter((item) => item.id !== action.id);
  }
}

export function useItemActions(
  items: Item[],
  groupId: string,
  onRefresh: () => void,
) {
  const [optimisticItems, addOptimistic] = useOptimistic(items, reducer);
  const [, startToggleTransition] = useTrackedTransition("items/toggle");
  const [, startDeleteTransition] = useTrackedTransition("items/delete");

  const toggle = useCallback(
    (item: Item) => {
      startToggleTransition(async () => {
        addOptimistic({ type: "toggle", id: item.id });
        try {
          await toggleItem(item);
        } catch {
          // Optimistic update auto-reverts on next render with fresh items
        }
        invalidateChecklists();
        await onRefresh();
      });
    },
    [addOptimistic, startToggleTransition, onRefresh],
  );

  const remove = useCallback(
    (item: Item) => {
      startDeleteTransition(async () => {
        addOptimistic({ type: "remove", id: item.id });
        try {
          await deleteItem(groupId, item.id);
        } catch {
          // Optimistic update auto-reverts on next render with fresh items
        }
        invalidateChecklists();
        await onRefresh();
      });
    },
    [addOptimistic, startDeleteTransition, groupId, onRefresh],
  );

  return { items: optimisticItems, toggle, remove };
}
