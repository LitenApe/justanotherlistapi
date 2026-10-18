import { useId, useRef, useState, type SubmitEvent } from "react";
import type { Item, ItemInput } from "../api/types";
import { Dialog, DialogActions } from "../components/Dialog";
import { Icon } from "../components/Icon";
import styles from "./ListPage.module.css";

interface EditItemDialogProps {
  item: Item | null;
  onSave: (item: Item, changes: Pick<ItemInput, "name" | "description">) => Promise<void>;
  onDelete: (item: Item) => void;
  onClose: () => void;
}

export function EditItemDialog({ item, onSave, onDelete, onClose }: EditItemDialogProps) {
  return (
    <Dialog open={item !== null} title="Edit item" onClose={onClose}>
      {/* Keyed so the draft resets for each item. */}
      {item && (
        <EditItemForm
          key={item.id}
          item={item}
          onSave={onSave}
          onDelete={onDelete}
          onClose={onClose}
        />
      )}
    </Dialog>
  );
}

function EditItemForm({ item, onSave, onDelete, onClose }: EditItemDialogProps & { item: Item }) {
  const [name, setName] = useState(item.name);
  const [description, setDescription] = useState(item.description ?? "");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const nameRef = useRef<HTMLInputElement>(null);
  const nameErrorId = useId();

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    const trimmed = name.trim();
    if (!trimmed) {
      setError("The item needs a name.");
      nameRef.current?.focus();
      return;
    }
    setPending(true);
    try {
      await onSave(item, { name: trimmed, description: description.trim() || null });
    } catch {
      // The caller reports failures; keep the dialog open so nothing is lost.
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
      <div className="field">
        <label className="label" htmlFor="edit-item-name">
          Name
        </label>
        <input
          ref={nameRef}
          id="edit-item-name"
          className="input"
          value={name}
          onChange={(event) => {
            setName(event.target.value);
          }}
          required
          autoComplete="off"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? nameErrorId : undefined}
          data-autofocus
        />
        {error && (
          <p className="field-error" id={nameErrorId}>
            {error}
          </p>
        )}
      </div>
      <div className="field">
        <label className="label" htmlFor="edit-item-description">
          Notes <span className="muted">(optional)</span>
        </label>
        <textarea
          id="edit-item-description"
          className="input"
          value={description}
          onChange={(event) => {
            setDescription(event.target.value);
          }}
          rows={4}
        />
      </div>
      <DialogActions>
        <button
          type="button"
          className={`button button-quiet button-danger-text ${styles.deleteAction}`}
          onClick={() => {
            onDelete(item);
          }}
        >
          <Icon name="trash" size={18} />
          Delete item
        </button>
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
