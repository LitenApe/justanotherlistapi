import type { Item } from "@shared/types";
import { deleteItem } from "./api";
import { useItemsOptimistic } from "./hooks";
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
  const { items: optimisticItems, toggle } = useItemsOptimistic(
    items,
    onRefresh,
  );
  const navigate = useNavigate();

  async function remove(item: Item) {
    await deleteItem(groupId, item.id);
    onRefresh();
  }

  function edit(item: Item) {
    navigate(`/${groupId}/items/${item.id}`);
  }

  return { optimisticItems, toggle, edit, remove };
}
