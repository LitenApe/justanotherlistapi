import type { Item } from "@shared/types";
import { Link } from "react-router";
import { RenderCount } from "@shared/components";
import { memo } from "react";
import { routes } from "@shared/routes";
import styles from "./ItemList.module.css";
import { useItemActions } from "./hooks";

// ─── Model ────────────────────────────────────────────────────────────────────

interface ItemListModel {
  optimisticItems: Item[];
  toggle: (item: Item) => void;
  remove: (item: Item) => void;
}

function useItemListModel(items: Item[], groupId: string): ItemListModel {
  const {
    items: optimisticItems,
    toggle,
    remove,
  } = useItemActions(items, groupId);

  return { optimisticItems, toggle, remove };
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface ItemRowProps {
  item: Item;
  groupId: string;
  onToggle: (item: Item) => void;
  onDelete: (item: Item) => void;
}

const ItemRow = memo(function ItemRow({
  item,
  groupId,
  onToggle,
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
        <Link
          to={routes.itemEdit(groupId, item.id)}
          className={styles.actionBtn}
          aria-label={`Edit ${item.name}`}
        >
          ✎
        </Link>
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

interface ItemListViewProps {
  items: Item[];
  groupId: string;
  onToggle: (item: Item) => void;
  onDelete: (item: Item) => void;
}

function ItemListView({
  items,
  groupId,
  onToggle,
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
          groupId={groupId}
          onToggle={onToggle}
          onDelete={onDelete}
        />
      ))}
    </div>
  );
}

// ─── Controller ───────────────────────────────────────────────────────────────

interface Props {
  items: Item[];
  groupId: string;
}

export function ItemList({ items, groupId }: Props) {
  const { optimisticItems, toggle, remove } = useItemListModel(items, groupId);
  return (
    <ItemListView
      items={optimisticItems}
      groupId={groupId}
      onToggle={toggle}
      onDelete={remove}
    />
  );
}
