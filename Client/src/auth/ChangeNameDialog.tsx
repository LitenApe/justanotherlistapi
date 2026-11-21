import { useId, useRef, useState, type SubmitEvent } from "react";
import { Dialog, DialogActions } from "../components/Dialog";
import { nameProblem } from "./accountRules";
import styles from "./AuthPage.module.css";

interface ChangeNameDialogProps {
  open: boolean;
  currentName: string;
  /** Saves the trimmed name; resolves to a problem to show, or null once it's saved. */
  onSave: (name: string) => Promise<string | null>;
  onClose: () => void;
}

/** Changes what you're called. Other members see the new name when they next load a list. */
export function ChangeNameDialog({ open, currentName, onSave, onClose }: ChangeNameDialogProps) {
  return (
    <Dialog
      open={open}
      title="Change your name"
      description="Other members of your lists see it the next time they open one."
      onClose={onClose}
    >
      {open && <NameForm currentName={currentName} onSave={onSave} onClose={onClose} />}
    </Dialog>
  );
}

function NameForm({ currentName, onSave, onClose }: Omit<ChangeNameDialogProps, "open">) {
  const [name, setName] = useState(currentName);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const errorId = useId();

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;

    const fail = (message: string) => {
      setError(message);
      inputRef.current?.focus();
    };

    const problem = nameProblem(name);
    if (problem) {
      fail(problem);
      return;
    }
    setError(null);
    setPending(true);
    const failure = await onSave(name.trim());
    if (failure) {
      setPending(false);
      fail(failure);
    }
  }

  return (
    <form
      className={styles.form}
      noValidate
      onSubmit={(event) => {
        void handleSubmit(event);
      }}
    >
      <div className="field">
        <label className="label" htmlFor="change-name">
          Name
        </label>
        <input
          ref={inputRef}
          id="change-name"
          className="input"
          value={name}
          onChange={(event) => {
            setName(event.target.value);
          }}
          required
          autoComplete="name"
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
