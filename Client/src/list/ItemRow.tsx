import { memo } from "react";
import type { Item } from "../api/types";
import { Icon } from "../components/Icon";
import styles from "./ListPage.module.css";

export const checkboxId = (itemId: string) => `item-${itemId}`;

interface ItemRowProps {
  item: Item;
  onToggle: (item: Item) => void;
  onEdit: (item: Item) => void;
}

export const ItemRow = memo(function ItemRow({ item, onToggle, onEdit }: ItemRowProps) {
  const id = checkboxId(item.id);
  const descriptionId = `${id}-description`;

  return (
    <li className={styles.item} data-complete={item.isComplete || undefined}>
      <input
        type="checkbox"
        id={id}
        className={styles.checkbox}
        checked={item.isComplete}
        onChange={() => {
          onToggle(item);
        }}
        aria-describedby={item.description ? descriptionId : undefined}
      />
      <div className={styles.itemText}>
        <label htmlFor={id} className={styles.itemName}>
          {item.name}
        </label>
        {item.description && (
          <p id={descriptionId} className={styles.itemDescription}>
            {item.description}
          </p>
        )}
      </div>
      <button
        type="button"
        className="icon-button"
        onClick={() => {
          onEdit(item);
        }}
      >
        <Icon name="pencil" size={18} />
        <span className="visually-hidden">Edit “{item.name}”</span>
      </button>
    </li>
  );
});
