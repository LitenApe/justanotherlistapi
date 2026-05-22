import { useCallback, useOptimistic, useTransition } from "react";

import type { Item } from "@shared/types";
import { toggleItem } from "./api";

type ToggleAction = { type: "toggle"; id: string };

function reducer(items: Item[], action: ToggleAction): Item[] {
  return items.map((item) =>
    item.id === action.id ? { ...item, isComplete: !item.isComplete } : item,
  );
}

export function useItemToggle(items: Item[], onRefresh: () => void) {
  const [optimisticItems, addOptimistic] = useOptimistic(items, reducer);
  const [, startTransition] = useTransition();

  const toggle = useCallback(
    (item: Item) => {
      startTransition(async () => {
        addOptimistic({ type: "toggle", id: item.id });
        try {
          await toggleItem(item);
        } catch {
          // Optimistic update auto-reverts on next render with fresh items
        }
        await onRefresh();
      });
    },
    [addOptimistic, startTransition, onRefresh],
  );

  return { items: optimisticItems, toggle };
}
