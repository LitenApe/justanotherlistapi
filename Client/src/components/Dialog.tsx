import { useEffect, useId, useRef, type ReactNode } from "react";
import styles from "./Dialog.module.css";

interface DialogProps {
  open: boolean;
  title: string;
  /** Called when the dialog closes, including via the Escape key. */
  onClose: () => void;
  children: ReactNode;
  description?: ReactNode;
}

/**
 * Modal dialog built on the native <dialog> element, which provides the focus
 * trap, Escape handling, inert background and top-layer rendering. Put
 * `data-autofocus` on the element that should receive focus when it opens.
 */
export function Dialog({ open, title, onClose, children, description }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const opener = useRef<Element | null>(null);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) {
      opener.current = document.activeElement;
      dialog.showModal();
      dialog.querySelector<HTMLElement>("[data-autofocus]")?.focus();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  function handleClose() {
    onClose();
    // Return focus to whatever opened the dialog, unless the caller already
    // moved it (for example because the opener was deleted).
    const target = opener.current;
    requestAnimationFrame(() => {
      const focusLost = document.activeElement === null || document.activeElement === document.body;
      if (focusLost && target instanceof HTMLElement && target.isConnected) target.focus();
    });
  }

  return (
    <dialog
      ref={ref}
      className={styles.dialog}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onClose={handleClose}
    >
      {open && (
        <div className={styles.body}>
          <h2 id={titleId} className={styles.title}>
            {title}
          </h2>
          {description && (
            <div id={descriptionId} className={styles.description}>
              {description}
            </div>
          )}
          {children}
        </div>
      )}
    </dialog>
  );
}

export function DialogActions({ children }: { children: ReactNode }) {
  return <div className={styles.actions}>{children}</div>;
}
