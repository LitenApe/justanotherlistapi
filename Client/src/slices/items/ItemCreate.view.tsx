import styles from "./ItemForm.module.css";

export interface ItemCreateViewProps {
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
  cancel: () => void;
}

export function ItemCreateView({
  error,
  isPending,
  formAction,
  cancel,
}: ItemCreateViewProps) {
  return (
    <form action={formAction} className={styles.form}>
      <h2 className={styles.title}>New Item</h2>

      {error && <p className={styles.error}>{error}</p>}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="name">
          Name
        </label>
        <input
          id="name"
          name="name"
          type="text"
          className={styles.input}
          required
        />
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="description">
          Description
        </label>
        <textarea
          id="description"
          name="description"
          className={styles.textarea}
          rows={3}
        />
      </div>

      <div className={styles.actions}>
        <button type="button" className={styles.cancelBtn} onClick={cancel}>
          Cancel
        </button>
        <button type="submit" className={styles.submitBtn} disabled={isPending}>
          {isPending ? "Creating…" : "Create"}
        </button>
      </div>
    </form>
  );
}
