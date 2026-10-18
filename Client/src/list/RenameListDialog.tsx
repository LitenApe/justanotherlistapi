import { useId, useRef, useState, type SubmitEvent } from "react";
import { Dialog, DialogActions } from "../components/Dialog";
import styles from "./ListPage.module.css";

interface RenameListDialogProps {
  open: boolean;
  currentName: string;
  onRename: (name: string) => Promise<void>;
  onClose: () => void;
}

export function RenameListDialog({ open, currentName, onRename, onClose }: RenameListDialogProps) {
  return (
    <Dialog open={open} title="Rename list" onClose={onClose}>
      {open && <RenameForm currentName={currentName} onRename={onRename} onClose={onClose} />}
    </Dialog>
  );
}

function RenameForm({ currentName, onRename, onClose }: Omit<RenameListDialogProps, "open">) {
  const [name, setName] = useState(currentName);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const errorId = useId();

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;
    const trimmed = name.trim();
    if (!trimmed) {
      setError("The list needs a name.");
      inputRef.current?.focus();
      return;
    }
    setPending(true);
    try {
      await onRename(trimmed);
    } catch {
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
        <label className="label" htmlFor="rename-list">
          List name
        </label>
        <input
          ref={inputRef}
          id="rename-list"
          className="input"
          value={name}
          onChange={(event) => {
            setName(event.target.value);
          }}
          required
          autoComplete="off"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          data-autofocus
        />
        {error && (
          <p className="field-error" id={errorId}>
            {error}
          </p>
        )}
      </div>
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
