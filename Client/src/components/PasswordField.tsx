import { useId, useState, type Ref } from "react";
import styles from "./PasswordField.module.css";

interface PasswordFieldProps {
  label: string;
  name: string;
  /** "new-password" when choosing one, so password managers offer to make and save it. */
  autoComplete: "current-password" | "new-password";
  hint?: string | undefined;
  error?: string | null | undefined;
  inputRef?: Ref<HTMLInputElement> | undefined;
}

/**
 * A password input with a visible label and a toggle that shows the password
 * as typed, so people can check it without retyping. The toggle keeps its name
 * and says whether it's on with aria-pressed.
 */
export function PasswordField({
  label,
  name,
  autoComplete,
  hint,
  error,
  inputRef,
}: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);
  const inputId = useId();
  const hintId = useId();
  const errorId = useId();
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ");

  return (
    <div className="field">
      <label className="label" htmlFor={inputId}>
        {label}
      </label>
      {hint && (
        <p className="hint" id={hintId}>
          {hint}
        </p>
      )}
      <div className={styles.row}>
        <input
          ref={inputRef}
          id={inputId}
          name={name}
          type={visible ? "text" : "password"}
          className={`input ${styles.input}`}
          autoComplete={autoComplete}
          autoCapitalize="none"
          autoCorrect="off"
          spellCheck={false}
          required
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy || undefined}
        />
        <button
          type="button"
          className="button"
          aria-pressed={visible}
          aria-controls={inputId}
          onClick={() => {
            setVisible((shown) => !shown);
          }}
        >
          Show password
        </button>
      </div>
      {error && (
        <p className="field-error" id={errorId}>
          {error}
        </p>
      )}
    </div>
  );
}
