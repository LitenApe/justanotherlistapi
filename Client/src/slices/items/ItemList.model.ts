import { useItemsLegacy, useItemsOptimistic } from "./hooks";

import type { Item } from "@shared/types";
import { deleteItem } from "./api";
import { routes } from "@shared/routes";
import { useCallback } from "react";
import { useFeatures } from "../dev-panel";
import { useNavigate } from "react-router";

export interface ItemListModel {
  optimisticItems: Item[];
  toggle: (item: Item) => void;
  edit: (item: Item) => void;
  remove: (item: Item) => void;
}

export function useItemListModel(
  items: Item[],
  groupId: string,
  onRefresh: () => void,
): ItemListModel {
  const { flags } = useFeatures();
  const optimistic = useItemsOptimistic(items, onRefresh);
  const legacy = useItemsLegacy(items, onRefresh);
  const { items: optimisticItems, toggle } = flags.useOptimistic
    ? optimistic
    : legacy;
  const navigate = useNavigate();

  const remove = useCallback(
    async (item: Item) => {
      await deleteItem(groupId, item.id);
      onRefresh();
    },
    [groupId, onRefresh],
  );

  const edit = useCallback(
    (item: Item) => {
      navigate(routes.itemEdit(groupId, item.id));
    },
    [navigate, groupId],
  );

  return { optimisticItems, toggle, edit, remove };
}
