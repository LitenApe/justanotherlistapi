import type { ReactNode } from "react";
import { Dialog, DialogActions } from "./Dialog";

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description: ReactNode;
  confirmLabel: string;
  pending?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/** Confirmation for destructive actions. Focus starts on Cancel, the safe choice. */
export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  pending = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Dialog open={open} title={title} description={description} onClose={onCancel}>
      <DialogActions>
        <button type="button" className="button" onClick={onCancel} data-autofocus>
          Cancel
        </button>
        <button
          type="button"
          className="button button-danger"
          aria-disabled={pending}
          onClick={() => {
            if (!pending) onConfirm();
          }}
        >
          {pending ? "Working…" : confirmLabel}
        </button>
      </DialogActions>
    </Dialog>
  );
}
