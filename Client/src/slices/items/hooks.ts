import { useCallback, useOptimistic } from "react";

import type { Item } from "@shared/types";
import { toggleItem } from "./api";

type ToggleAction = { type: "toggle"; id: string };

function reducer(items: Item[], action: ToggleAction): Item[] {
  return items.map((item) =>
    item.id === action.id ? { ...item, isComplete: !item.isComplete } : item,
  );
}

export function useItemsOptimistic(items: Item[], onRefresh: () => void) {
  const [optimisticItems, addOptimistic] = useOptimistic(items, reducer);

  const toggle = useCallback(
    async (item: Item) => {
      addOptimistic({ type: "toggle", id: item.id });
      try {
        await toggleItem(item);
        onRefresh();
      } catch {
        // Optimistic update auto-reverts on next render with fresh items
        onRefresh();
      }
    },
    [addOptimistic, onRefresh],
  );

  return { items: optimisticItems, toggle };
}
