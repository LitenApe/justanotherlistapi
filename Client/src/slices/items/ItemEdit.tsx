import { use } from "react";

import { checklistsResource } from "@shared/api";
import { invalidateChecklists } from "@slices/checklists";
import { invalidateDetail } from "@slices/checklist-detail";
import { routes } from "@shared/routes";
import styles from "./ItemForm.module.css";
import { updateItem } from "./api";
import { useNavigate } from "react-router";
import { useTrackedActionState } from "@shared/hooks";

// ─── Model ────────────────────────────────────────────────────────────────────

const itemCache = new Map<
  string,
  Promise<{ name: string; description: string | null; isComplete: boolean }>
>();

function getItemPromise(groupId: string, itemId: string) {
  const key = `${groupId}/${itemId}`;
  let promise = itemCache.get(key);
  if (!promise) {
    promise = checklistsResource.getById(groupId).then((group) => {
      const item = group.items.find((i) => i.id === itemId);
      if (!item) throw new Error("Item not found");
      return item;
    });
    itemCache.set(key, promise);
  }
  return promise;
}

interface ItemEditModel {
  item: { name: string; description: string | null; isComplete: boolean };
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
  cancel: () => void;
}

function useItemEditModel(groupId: string, itemId: string): ItemEditModel {
  const item = use(getItemPromise(groupId, itemId));
  const navigate = useNavigate();

  const [state, formAction, isPending] = useTrackedActionState<
    { error: string | null },
    FormData
  >(
    "items/edit-form",
    async (_prev, formData) => {
      const name = (formData.get("name") as string).trim();
      const description =
        (formData.get("description") as string).trim() || undefined;
      const isComplete = formData.get("isComplete") === "on";

      if (!name) return { error: "Name is required" };

      try {
        await updateItem(groupId, itemId, {
          name,
          ...(description && { description }),
          isComplete,
        });
        itemCache.delete(`${groupId}/${itemId}`);
        invalidateChecklists();
        invalidateDetail(groupId);
        navigate(routes.checklist(groupId), { replace: true });
        return { error: null };
      } catch (e) {
        return {
          error: e instanceof Error ? e.message : "Failed to update item",
        };
      }
    },
    { error: null },
  );

  function cancel() {
    navigate(routes.checklist(groupId));
  }

  return { item, error: state.error, isPending, formAction, cancel };
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface ItemEditViewProps {
  item: { name: string; description: string | null; isComplete: boolean };
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
  cancel: () => void;
}

function ItemEditView({
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

// ─── Controller ───────────────────────────────────────────────────────────────

interface Props {
  groupId: string;
  itemId: string;
}

export function ItemEdit({ groupId, itemId }: Props) {
  const model = useItemEditModel(groupId, itemId);
  return <ItemEditView {...model} />;
}
