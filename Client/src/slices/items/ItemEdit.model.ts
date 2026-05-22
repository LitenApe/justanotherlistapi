import { use, useActionState } from "react";

import { checklistsResource } from "@shared/api";
import { routes } from "@shared/routes";
import { updateItem } from "./api";
import { useNavigate } from "react-router";

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

export interface ItemEditModel {
  item: { name: string; description: string | null; isComplete: boolean };
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
  cancel: () => void;
}

export function useItemEditModel(
  groupId: string,
  itemId: string,
): ItemEditModel {
  const item = use(getItemPromise(groupId, itemId));
  const navigate = useNavigate();

  const [state, formAction, isPending] = useActionState<
    { error: string | null },
    FormData
  >(
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
