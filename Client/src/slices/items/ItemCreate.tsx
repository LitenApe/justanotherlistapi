import { createItem } from "./api";
import { routes } from "@shared/routes";
import styles from "./ItemForm.module.css";
import { useNavigate } from "react-router";
import { useTrackedActionState } from "@shared/hooks";

// ─── Model ────────────────────────────────────────────────────────────────────

interface ItemCreateModel {
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
  cancel: () => void;
}

function useItemCreateModel(groupId: string): ItemCreateModel {
  const navigate = useNavigate();

  const [state, formAction, isPending] = useTrackedActionState<
    { error: string | null },
    FormData
  >(
    "items/create-form",
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
        navigate(routes.checklist(groupId), { replace: true });
        return { error: null };
      } catch (e) {
        return {
          error: e instanceof Error ? e.message : "Failed to create item",
        };
      }
    },
    { error: null },
  );

  function cancel() {
    navigate(routes.checklist(groupId));
  }

  return { error: state.error, isPending, formAction, cancel };
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface ItemCreateViewProps {
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
  cancel: () => void;
}

function ItemCreateView({
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

// ─── Controller ───────────────────────────────────────────────────────────────

interface Props {
  groupId: string;
}

export function ItemCreate({ groupId }: Props) {
  const model = useItemCreateModel(groupId);
  return <ItemCreateView {...model} />;
}
