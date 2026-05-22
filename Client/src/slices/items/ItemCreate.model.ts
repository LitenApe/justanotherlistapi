import { createItem } from "./api";
import { useActionState } from "react";
import { useNavigate } from "react-router";

export interface ItemCreateModel {
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
  cancel: () => void;
}

export function useItemCreateModel(groupId: string): ItemCreateModel {
  const navigate = useNavigate();

  const [state, formAction, isPending] = useActionState<
    { error: string | null },
    FormData
  >(
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

  function cancel() {
    navigate(`/${groupId}`);
  }

  return { error: state.error, isPending, formAction, cancel };
}
