import { memo } from "react";
import type { Item } from "../api/types";
import { Icon } from "../components/Icon";
import styles from "./ListPage.module.css";

export const checkboxId = (itemId: string) => `item-${itemId}`;

interface ItemRowProps {
  item: Item;
  /** Who the item is reserved for, as text (see reservationText), or null while it's free. */
  reservation: string | null;
  /** Whether the user may reserve the item, or change its reservation. */
  canReserve: boolean;
  onToggle: (item: Item) => void;
  onEdit: (item: Item) => void;
  onReserve: (item: Item) => void;
}

export const ItemRow = memo(function ItemRow({
  item,
  reservation,
  canReserve,
  onToggle,
  onEdit,
  onReserve,
}: ItemRowProps) {
  const id = checkboxId(item.id);
  const descriptionId = `${id}-description`;
  const reservationId = `${id}-reservation`;
  const describedBy = [item.description && descriptionId, reservation && reservationId]
    .filter(Boolean)
    .join(" ");

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
        aria-describedby={describedBy || undefined}
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
        {reservation && (
          <p id={reservationId} className={styles.itemReservation}>
            <Icon name="userCheck" size={16} />
            {reservation}
          </p>
        )}
      </div>
      <div className={styles.itemActions}>
        {canReserve && (
          <button
            type="button"
            className="icon-button"
            onClick={() => {
              onReserve(item);
            }}
          >
            <Icon name="userCheck" size={18} />
            <span className="visually-hidden">
              {item.reservedFor === null
                ? `Reserve “${item.name}”`
                : `Change reservation for “${item.name}”`}
            </span>
          </button>
        )}
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
      </div>
    </li>
  );
});
