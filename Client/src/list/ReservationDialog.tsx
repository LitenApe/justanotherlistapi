import { useState, type SubmitEvent } from "react";
import type { Item, Member } from "../api/types";
import { Dialog, DialogActions } from "../components/Dialog";
import styles from "./ListPage.module.css";
import { memberName } from "./members";
import { reservationText } from "./reservations";

interface ReservationDialogProps {
  /** The item to reserve, or null while the dialog is closed. */
  item: Item | null;
  userId: string;
  members: readonly Member[];
  /**
   * Saves the choice: who to reserve the item for (null to cancel the
   * reservation), and who it was reserved for when the dialog opened, so a
   * change someone made meanwhile isn't overwritten.
   */
  onSave: (item: Item, memberId: string | null, seen: string | null) => Promise<void>;
  onClose: () => void;
}

export function ReservationDialog({
  item,
  userId,
  members,
  onSave,
  onClose,
}: ReservationDialogProps) {
  return (
    <Dialog
      open={item !== null}
      title={item ? `Reserve “${item.name}”` : "Reserve item"}
      description="Say who will do it, so nobody else picks it."
      onClose={onClose}
    >
      {/* Keyed so the choice, and the reservation it replaces, reset for each item. */}
      {item && (
        <ReservationForm
          key={item.id}
          item={item}
          userId={userId}
          members={members}
          onSave={onSave}
          onClose={onClose}
        />
      )}
    </Dialog>
  );
}

/** The radio value for "nobody"; member IDs are GUIDs, so it can't clash. */
const NOBODY = "nobody";

function ReservationForm({
  item,
  userId,
  members,
  onSave,
  onClose,
}: ReservationDialogProps & { item: Item }) {
  const [seen] = useState(item.reservedFor);
  const [choice, setChoice] = useState(item.reservedFor ?? userId);
  const [pending, setPending] = useState(false);
  const current = reservationText(item, userId, members);
  const others = members
    .filter((member) => member.id !== userId)
    .map((member) => ({ value: member.id, label: memberName(member.id, members) }))
    .toSorted((a, b) => a.label.localeCompare(b.label));
  const options = [
    ...(seen === null ? [] : [{ value: NOBODY, label: "Nobody" }]),
    { value: userId, label: "You" },
    ...others,
  ];

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    setPending(true);
    try {
      await onSave(item, choice === NOBODY ? null : choice, seen);
    } catch {
      // The caller reports failures; keep the dialog open to try again.
      setPending(false);
    }
  }

  return (
    <form
      className={styles.dialogForm}
      noValidate
      onSubmit={(event) => {
        void handleSubmit(event);
      }}
    >
      <p className="hint">{current ? `${current}.` : "Nobody has reserved it yet."}</p>
      <fieldset className={styles.reserveOptions}>
        <legend className="label">Reserved for</legend>
        {options.map(({ value, label }) => (
          <label key={value} className={styles.reserveOption}>
            <input
              type="radio"
              name="reserved-for"
              value={value}
              checked={choice === value}
              onChange={() => {
                setChoice(value);
              }}
              data-autofocus={choice === value || undefined}
            />
            <span>{label}</span>
          </label>
        ))}
      </fieldset>
      <DialogActions>
        <button type="button" className="button" onClick={onClose}>
          Cancel
        </button>
        <button type="submit" className="button button-primary" aria-disabled={pending}>
          {pending ? "Saving…" : "Save"}
        </button>
      </DialogActions>
    </form>
  );
}
