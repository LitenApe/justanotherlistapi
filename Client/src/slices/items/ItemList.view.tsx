import type { Item } from "@shared/types";
import { memo } from "react";
import { RenderCount } from "@shared/components";
import styles from "./ItemList.module.css";

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
      <RenderCount label={`Item:${item.name}`} />
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

export interface ItemListViewProps {
  items: Item[];
  onToggle: (item: Item) => void;
  onEdit: (item: Item) => void;
  onDelete: (item: Item) => void;
}

export function ItemListView({
  items,
  onToggle,
  onEdit,
  onDelete,
}: ItemListViewProps) {
  if (items.length === 0) {
    return <p className={styles.empty}>No items yet. Add one above!</p>;
  }

  return (
    <div className={styles.list} role="list" aria-label="Items">
      <RenderCount label="ItemList" />
      {items.map((item) => (
        <ItemRow
          key={item.id}
          item={item}
          onToggle={onToggle}
          onEdit={onEdit}
          onDelete={onDelete}
        />
      ))}
    </div>
  );
}
