import { createChecklist, deleteChecklist } from "./api";
import { use, useCallback, useOptimistic } from "react";

import type { ItemGroup } from "@shared/types";
import { checklistsResource } from "@shared/api";
import { useTrackedTransition } from "@shared/hooks";

let checklistsPromise: Promise<ItemGroup[]> | null = null;

function getChecklistsPromise(): Promise<ItemGroup[]> {
  if (!checklistsPromise) {
    checklistsPromise = checklistsResource.getAll();
  }
  return checklistsPromise;
}

export function invalidateChecklists(): void {
  checklistsPromise = null;
}

type ChecklistAction = { type: "remove"; id: string };

function checklistReducer(
  checklists: ItemGroup[],
  action: ChecklistAction,
): ItemGroup[] {
  switch (action.type) {
    case "remove":
      return checklists.filter((c) => c.id !== action.id);
  }
}

export function useChecklists() {
  const checklists = use(getChecklistsPromise());
  const [optimisticChecklists, addOptimistic] = useOptimistic(
    checklists,
    checklistReducer,
  );
  const [isPending, startTransition] = useTrackedTransition(
    "checklists/mutation",
  );

  const refresh = useCallback(() => {
    startTransition(async () => {
      invalidateChecklists();
      await getChecklistsPromise();
    });
  }, [startTransition]);

  const add = useCallback(
    (name: string): Promise<ItemGroup> => {
      return new Promise((resolve, reject) => {
        startTransition(async () => {
          try {
            const created = await createChecklist(name);
            invalidateChecklists();
            await getChecklistsPromise();
            resolve(created);
          } catch (e) {
            reject(e);
          }
        });
      });
    },
    [startTransition],
  );

  const remove = useCallback(
    (id: string) => {
      startTransition(async () => {
        addOptimistic({ type: "remove", id });
        await deleteChecklist(id);
        invalidateChecklists();
        await getChecklistsPromise();
      });
    },
    [startTransition, addOptimistic],
  );

  return { checklists: optimisticChecklists, isPending, refresh, add, remove };
}
