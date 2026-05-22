import type { Item } from "@shared/types";
import { ItemListView } from "./ItemList.view";
import { useItemListModel } from "./ItemList.model";

interface Props {
  items: Item[];
  groupId: string;
  onRefresh: () => void;
}

export function ItemList({ items, groupId, onRefresh }: Props) {
  const { optimisticItems, toggle, edit, remove } = useItemListModel(
    items,
    groupId,
    onRefresh,
  );
  return (
    <ItemListView
      items={optimisticItems}
      onToggle={toggle}
      onEdit={edit}
      onDelete={remove}
    />
  );
}
