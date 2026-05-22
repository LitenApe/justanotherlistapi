import { createItem } from "./api";
import styles from "./ItemForm.module.css";
import { useActionState } from "react";
import { useNavigate } from "react-router";

interface Props {
  groupId: string;
}

interface FormState {
  error: string | null;
}

export function ItemCreate({ groupId }: Props) {
  const navigate = useNavigate();

  const [state, formAction, isPending] = useActionState<FormState, FormData>(
    async (_prev, formData) => {
      const name = (formData.get("name") as string).trim();
      const description =
        (formData.get("description") as string).trim() || undefined;

      if (!name) return { error: "Name is required" };

      try {
        await createItem(groupId, {
          name,
          ...(description && { description }),
        });
        navigate(`/${groupId}`, { replace: true });
        return { error: null };
      } catch (e) {
        return {
          error: e instanceof Error ? e.message : "Failed to create item",
        };
      }
    },
    { error: null },
  );

  return (
    <form action={formAction} className={styles.form}>
      <h2 className={styles.title}>New Item</h2>

      {state.error && <p className={styles.error}>{state.error}</p>}

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
        <button
          type="button"
          className={styles.cancelBtn}
          onClick={() => navigate(`/${groupId}`)}
        >
          Cancel
        </button>
        <button type="submit" className={styles.submitBtn} disabled={isPending}>
          {isPending ? "Creating…" : "Create"}
        </button>
      </div>
    </form>
  );
}
