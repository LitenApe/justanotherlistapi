import { deleteItem, toggleItem } from "./api";
import { useCallback, useOptimistic } from "react";

import type { Item } from "@shared/types";
import { invalidateChecklists } from "@slices/checklists";
import { updateDetailItems } from "@slices/checklist-detail";
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

export function useItemActions(items: Item[], groupId: string) {
  const [optimisticItems, addOptimistic] = useOptimistic(items, reducer);
  const [, startToggleTransition] = useTrackedTransition("items/toggle");
  const [, startDeleteTransition] = useTrackedTransition("items/delete");

  const toggle = useCallback(
    (item: Item) => {
      startToggleTransition(async () => {
        addOptimistic({ type: "toggle", id: item.id });
        await toggleItem(item);
        updateDetailItems(groupId, (items) =>
          items.map((i) =>
            i.id === item.id ? { ...i, isComplete: !item.isComplete } : i,
          ),
        );
        invalidateChecklists();
      });
    },
    [addOptimistic, startToggleTransition, groupId],
  );

  const remove = useCallback(
    (item: Item) => {
      startDeleteTransition(async () => {
        addOptimistic({ type: "remove", id: item.id });
        await deleteItem(groupId, item.id);
        updateDetailItems(groupId, (items) =>
          items.filter((i) => i.id !== item.id),
        );
        invalidateChecklists();
      });
    },
    [addOptimistic, startDeleteTransition, groupId],
  );

  return { items: optimisticItems, toggle, remove };
}
