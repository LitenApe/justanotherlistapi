import { memo, useCallback } from "react";

import type { Item } from "@shared/types";
import { RenderCount } from "@shared/components";
import { deleteItem } from "./api";
import { routes } from "@shared/routes";
import styles from "./ItemList.module.css";
import { useItemToggle } from "./hooks";
import { useNavigate } from "react-router";

// ─── Model ────────────────────────────────────────────────────────────────────

interface ItemListModel {
  optimisticItems: Item[];
  toggle: (item: Item) => void;
  edit: (item: Item) => void;
  remove: (item: Item) => void;
}

function useItemListModel(
  items: Item[],
  groupId: string,
  onRefresh: () => void,
): ItemListModel {
  const { items: optimisticItems, toggle } = useItemToggle(items, onRefresh);
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

// ─── View ─────────────────────────────────────────────────────────────────────

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

interface ItemListViewProps {
  items: Item[];
  onToggle: (item: Item) => void;
  onEdit: (item: Item) => void;
  onDelete: (item: Item) => void;
}

function ItemListView({
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

// ─── Controller ───────────────────────────────────────────────────────────────

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
