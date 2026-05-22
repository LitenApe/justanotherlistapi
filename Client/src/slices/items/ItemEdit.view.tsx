import styles from "./ItemForm.module.css";

export interface ItemEditViewProps {
  item: { name: string; description: string | null; isComplete: boolean };
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
  cancel: () => void;
}

export function ItemEditView({
  item,
  error,
  isPending,
  formAction,
  cancel,
}: ItemEditViewProps) {
  return (
    <form action={formAction} className={styles.form}>
      <h2 className={styles.title}>Edit Item</h2>

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
          defaultValue={item.name}
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
          defaultValue={item.description ?? ""}
        />
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="isComplete">
          <input
            id="isComplete"
            name="isComplete"
            type="checkbox"
            defaultChecked={item.isComplete}
          />{" "}
          Completed
        </label>
      </div>

      <div className={styles.actions}>
        <button type="button" className={styles.cancelBtn} onClick={cancel}>
          Cancel
        </button>
        <button type="submit" className={styles.submitBtn} disabled={isPending}>
          {isPending ? "Saving…" : "Save"}
        </button>
      </div>
    </form>
  );
}
