import { use, useActionState } from "react";

import { checklistsResource } from "@shared/api";
import styles from "./ItemForm.module.css";
import { updateItem } from "./api";
import { useNavigate } from "react-router";

interface Props {
  groupId: string;
  itemId: string;
}

interface FormState {
  error: string | null;
}

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

export function ItemEdit({ groupId, itemId }: Props) {
  const item = use(getItemPromise(groupId, itemId));
  const navigate = useNavigate();

  const [state, formAction, isPending] = useActionState<FormState, FormData>(
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
        navigate(`/${groupId}`, { replace: true });
        return { error: null };
      } catch (e) {
        return {
          error: e instanceof Error ? e.message : "Failed to update item",
        };
      }
    },
    { error: null },
  );

  return (
    <form action={formAction} className={styles.form}>
      <h2 className={styles.title}>Edit Item</h2>

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
        <button
          type="button"
          className={styles.cancelBtn}
          onClick={() => navigate(`/${groupId}`)}
        >
          Cancel
        </button>
        <button type="submit" className={styles.submitBtn} disabled={isPending}>
          {isPending ? "Saving…" : "Save"}
        </button>
      </div>
    </form>
  );
}
