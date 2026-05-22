import type { Item } from "@shared/types";
import { deleteItem } from "./api";
import { memo } from "react";
import styles from "./ItemList.module.css";
import { useItemsOptimistic } from "./hooks";
import { useNavigate } from "react-router";

interface ItemRowProps {
  item: Item;
  onToggle: (item: Item) => void;
  onEdit: (item: Item) => void;
  onDelete: (item: Item) => void;
}

const ItemRow = memo(function ItemRow({
  item,
  onToggle,
  onEdit,
  onDelete,
}: ItemRowProps) {
  return (
    <div className={styles.row}>
      <input
        type="checkbox"
        className={styles.checkbox}
        checked={item.isComplete}
        onChange={() => onToggle(item)}
        aria-label={`Mark ${item.name} as ${item.isComplete ? "incomplete" : "complete"}`}
      />
      <div className={styles.name}>
        <span className={item.isComplete ? styles.complete : undefined}>
          {item.name}
        </span>
        {item.description && (
          <p className={styles.description}>{item.description}</p>
        )}
      </div>
      <div className={styles.actions}>
        <button
          type="button"
          className={styles.actionBtn}
          onClick={() => onEdit(item)}
          aria-label={`Edit ${item.name}`}
        >
          ✎
        </button>
        <button
          type="button"
          className={styles.actionBtn}
          onClick={() => onDelete(item)}
          aria-label={`Delete ${item.name}`}
        >
          ✕
        </button>
      </div>
    </div>
  );
});

interface Props {
  items: Item[];
  groupId: string;
  onRefresh: () => void;
}

export function ItemList({ items, groupId, onRefresh }: Props) {
  const { items: optimisticItems, toggle } = useItemsOptimistic(
    items,
    onRefresh,
  );
  const navigate = useNavigate();

  async function handleDelete(item: Item) {
    await deleteItem(groupId, item.id);
    onRefresh();
  }

  if (optimisticItems.length === 0) {
    return <p className={styles.empty}>No items yet. Add one above!</p>;
  }

  return (
    <div className={styles.list} role="list" aria-label="Items">
      {optimisticItems.map((item) => (
        <ItemRow
          key={item.id}
          item={item}
          onToggle={toggle}
          onEdit={(i) => navigate(`/${groupId}/items/${i.id}`)}
          onDelete={handleDelete}
        />
      ))}
    </div>
  );
}
